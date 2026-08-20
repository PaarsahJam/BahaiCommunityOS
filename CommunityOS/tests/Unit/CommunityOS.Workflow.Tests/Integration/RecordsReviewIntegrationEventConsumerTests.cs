using CommunityOS.Workflow.Domain.Aggregates;
using CommunityOS.Workflow.Domain.Repositories;
using CommunityOS.Workflow.Infrastructure.Integration.Records;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CommunityOS.Workflow.Tests.Integration;

/// <summary>
/// Reconciliation of <c>record-review</c> tasks from Records integration events
/// (ADR-024). The consumer is create-if-absent / close-if-open: duplicate
/// events never produce duplicate open tasks and never double-complete a
/// terminal task. Verify/reject events carry the authoritative outcome, but
/// Workflow never performs the Records governance action itself.
/// </summary>
public class RecordsReviewIntegrationEventConsumerTests
{
    private static readonly Guid RecordId = Guid.NewGuid();
    private static readonly Guid Reviewer = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static ConsumeContext<T> Context<T>(T message)
        where T : class
    {
        var ctx = Substitute.For<ConsumeContext<T>>();
        ctx.Message.Returns(message);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    private static (RecordsReviewIntegrationEventConsumer Consumer, IWorkflowTaskRepository Tasks) Build(
        IMediator mediator)
    {
        var tasks = Substitute.For<IWorkflowTaskRepository>();
        var consumer = new RecordsReviewIntegrationEventConsumer(
            tasks, mediator, NullLogger<RecordsReviewIntegrationEventConsumer>.Instance);
        return (consumer, tasks);
    }

    [Fact]
    public async Task RecordSubmitted_creates_record_review_task_if_absent()
    {
        var mediator = Substitute.For<IMediator>();
        var (consumer, tasks) = Build(mediator);
        tasks.FindOpenAsync(Arg.Any<string>(), Arg.Any<string>(), RecordId, Arg.Any<CancellationToken>())
            .Returns((WorkflowTask?)null);
        tasks.AddIfAbsentAsync(Arg.Any<WorkflowTask>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<WorkflowTask>());

        await consumer.Consume(Context(new CommunityOS.Contracts.Records.RecordSubmitted(RecordId, "submitted", Now)));

        await tasks.Received(1).FindOpenAsync(
            Arg.Any<string>(), Arg.Any<string>(), RecordId, Arg.Any<CancellationToken>());
        await tasks.Received(1).AddIfAbsentAsync(Arg.Any<WorkflowTask>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordSubmitted_does_not_create_when_open_task_exists()
    {
        var mediator = Substitute.For<IMediator>();
        var (consumer, tasks) = Build(mediator);
        var existing = WorkflowTask.Create(
            "record-review", "record", RecordId, null, [], [], null, null,
            Guid.NewGuid(), Guid.NewGuid(), Now);
        tasks.FindOpenAsync(Arg.Any<string>(), Arg.Any<string>(), RecordId, Arg.Any<CancellationToken>())
            .Returns(existing);

        await consumer.Consume(Context(new CommunityOS.Contracts.Records.RecordSubmitted(RecordId, "submitted", Now)));

        await tasks.DidNotReceive().AddIfAbsentAsync(Arg.Any<WorkflowTask>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordVerified_completes_open_task_with_verified_outcome()
    {
        var mediator = Substitute.For<IMediator>();
        var (consumer, tasks) = Build(mediator);
        var task = WorkflowTask.Create(
            "record-review", "record", RecordId, null, [], [], null, null,
            Guid.NewGuid(), Guid.NewGuid(), Now);
        tasks.FindOpenAsync(Arg.Any<string>(), Arg.Any<string>(), RecordId, Arg.Any<CancellationToken>())
            .Returns(task);

        await consumer.Consume(Context(new CommunityOS.Contracts.Records.RecordVerified(RecordId, Reviewer, Now)));

        task.Status.Should().Be(CommunityOS.Workflow.Domain.Enumerations.WorkflowStatus.Completed);
        task.Outcome.Should().Be("verified");
        task.CompletedBy.Should().Be(Reviewer);
        await tasks.Received(1).UpdateAsync(task, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordRejected_completes_open_task_with_rejected_outcome()
    {
        var mediator = Substitute.For<IMediator>();
        var (consumer, tasks) = Build(mediator);
        var task = WorkflowTask.Create(
            "record-review", "record", RecordId, null, [], [], null, null,
            Guid.NewGuid(), Guid.NewGuid(), Now);
        tasks.FindOpenAsync(Arg.Any<string>(), Arg.Any<string>(), RecordId, Arg.Any<CancellationToken>())
            .Returns(task);

        await consumer.Consume(Context(new CommunityOS.Contracts.Records.RecordRejected(RecordId, Reviewer, Now)));

        task.Status.Should().Be(CommunityOS.Workflow.Domain.Enumerations.WorkflowStatus.Completed);
        task.Outcome.Should().Be("rejected");
    }

    [Fact]
    public async Task RecordVerified_noops_when_no_open_task_exists()
    {
        var mediator = Substitute.For<IMediator>();
        var (consumer, tasks) = Build(mediator);
        tasks.FindOpenAsync(Arg.Any<string>(), Arg.Any<string>(), RecordId, Arg.Any<CancellationToken>())
            .Returns((WorkflowTask?)null);

        await consumer.Consume(Context(new CommunityOS.Contracts.Records.RecordVerified(RecordId, Reviewer, Now)));

        await tasks.DidNotReceive().UpdateAsync(Arg.Any<WorkflowTask>(), Arg.Any<CancellationToken>());
    }
}