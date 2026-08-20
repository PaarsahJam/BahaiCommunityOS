using CommunityOS.Workflow.Domain.Aggregates;
using CommunityOS.Workflow.Domain.Repositories;
using CommunityOS.Workflow.Infrastructure.Integration.Knowledge;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CommunityOS.Workflow.Tests.Integration;

/// <summary>
/// Reconciliation of <c>knowledge-moderation</c> and <c>knowledge-ai-review</c>
/// tasks from Knowledge integration events (ADR-024). Create-if-absent on
/// flag/under-review/ai-requested, close-if-open with the authoritative outcome
/// on merge/archive/review. The outcome is advisory: the governance action
/// itself is always performed through the Knowledge API, never by Workflow.
/// </summary>
public class KnowledgeReviewIntegrationEventConsumerTests
{
    private static readonly Guid QuestionId = Guid.NewGuid();
    private static readonly Guid SuggestionId = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static ConsumeContext<T> Context<T>(T message)
        where T : class
    {
        var ctx = Substitute.For<ConsumeContext<T>>();
        ctx.Message.Returns(message);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    private static (KnowledgeReviewIntegrationEventConsumer Consumer, IWorkflowTaskRepository Tasks) Build(
        IMediator mediator)
    {
        var tasks = Substitute.For<IWorkflowTaskRepository>();
        var consumer = new KnowledgeReviewIntegrationEventConsumer(
            tasks, mediator, NullLogger<KnowledgeReviewIntegrationEventConsumer>.Instance);
        return (consumer, tasks);
    }

    private static WorkflowTask OpenTask(string definitionCode, string domainType, Guid domainEntityId) =>
        WorkflowTask.Create(
            definitionCode, domainType, domainEntityId, null, [], [], null, null,
            Guid.NewGuid(), Guid.NewGuid(), Now);

    [Fact]
    public async Task QuestionFlagged_creates_moderation_task_if_absent()
    {
        var mediator = Substitute.For<IMediator>();
        var (consumer, tasks) = Build(mediator);
        tasks.FindOpenAsync(Arg.Any<string>(), Arg.Any<string>(), QuestionId, Arg.Any<CancellationToken>())
            .Returns((WorkflowTask?)null);
        tasks.AddIfAbsentAsync(Arg.Any<WorkflowTask>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<WorkflowTask>());

        await consumer.Consume(Context(new CommunityOS.Contracts.Knowledge.QuestionFlagged(QuestionId, "duplicate", Now)));

        await tasks.Received(1).AddIfAbsentAsync(
            Arg.Is<WorkflowTask>(t =>
                t.DefinitionCode == "knowledge-moderation" &&
                t.DomainType == "knowledge-question" &&
                t.DomainEntityId == QuestionId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QuestionUnderReview_does_not_duplicate_when_open_task_exists()
    {
        var mediator = Substitute.For<IMediator>();
        var (consumer, tasks) = Build(mediator);
        tasks.FindOpenAsync(Arg.Any<string>(), Arg.Any<string>(), QuestionId, Arg.Any<CancellationToken>())
            .Returns(OpenTask("knowledge-moderation", "knowledge-question", QuestionId));

        await consumer.Consume(Context(new CommunityOS.Contracts.Knowledge.QuestionUnderReview(QuestionId, Now)));

        await tasks.DidNotReceive().AddIfAbsentAsync(Arg.Any<WorkflowTask>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QuestionMerged_completes_open_moderation_task_with_merged_outcome()
    {
        var mediator = Substitute.For<IMediator>();
        var (consumer, tasks) = Build(mediator);
        var task = OpenTask("knowledge-moderation", "knowledge-question", QuestionId);
        tasks.FindOpenAsync(Arg.Any<string>(), Arg.Any<string>(), QuestionId, Arg.Any<CancellationToken>())
            .Returns(task);

        await consumer.Consume(Context(new CommunityOS.Contracts.Knowledge.QuestionMerged(QuestionId, Guid.NewGuid(), Now)));

        task.Status.Should().Be(CommunityOS.Workflow.Domain.Enumerations.WorkflowStatus.Completed);
        task.Outcome.Should().Be("merged");
        await tasks.Received(1).UpdateAsync(task, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QuestionArchived_completes_open_moderation_task_with_archived_outcome()
    {
        var mediator = Substitute.For<IMediator>();
        var (consumer, tasks) = Build(mediator);
        var task = OpenTask("knowledge-moderation", "knowledge-question", QuestionId);
        tasks.FindOpenAsync(Arg.Any<string>(), Arg.Any<string>(), QuestionId, Arg.Any<CancellationToken>())
            .Returns(task);

        await consumer.Consume(Context(new CommunityOS.Contracts.Knowledge.QuestionArchived(QuestionId, Now)));

        task.Outcome.Should().Be("archived");
        task.Status.Should().Be(CommunityOS.Workflow.Domain.Enumerations.WorkflowStatus.Completed);
    }

    [Fact]
    public async Task AiSuggestionRequested_creates_ai_review_task_if_absent()
    {
        var mediator = Substitute.For<IMediator>();
        var (consumer, tasks) = Build(mediator);
        tasks.FindOpenAsync(Arg.Any<string>(), Arg.Any<string>(), SuggestionId, Arg.Any<CancellationToken>())
            .Returns((WorkflowTask?)null);
        tasks.AddIfAbsentAsync(Arg.Any<WorkflowTask>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<WorkflowTask>());

        await consumer.Consume(Context(new CommunityOS.Contracts.Knowledge.AiSuggestionRequested(SuggestionId, QuestionId, "model-x", Now)));

        await tasks.Received(1).AddIfAbsentAsync(
            Arg.Is<WorkflowTask>(t =>
                t.DefinitionCode == "knowledge-ai-review" &&
                t.DomainType == "knowledge-ai-suggestion" &&
                t.DomainEntityId == SuggestionId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AiSuggestionReviewed_completes_open_ai_review_task_with_recorded_outcome()
    {
        var mediator = Substitute.For<IMediator>();
        var (consumer, tasks) = Build(mediator);
        var task = OpenTask("knowledge-ai-review", "knowledge-ai-suggestion", SuggestionId);
        tasks.FindOpenAsync(Arg.Any<string>(), Arg.Any<string>(), SuggestionId, Arg.Any<CancellationToken>())
            .Returns(task);

        await consumer.Consume(Context(new CommunityOS.Contracts.Knowledge.AiSuggestionReviewed(SuggestionId, QuestionId, "accepted", Now)));

        task.Outcome.Should().Be("accepted");
        task.Status.Should().Be(CommunityOS.Workflow.Domain.Enumerations.WorkflowStatus.Completed);
        await tasks.Received(1).UpdateAsync(task, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Close_if_open_noops_when_no_open_task_exists()
    {
        var mediator = Substitute.For<IMediator>();
        var (consumer, tasks) = Build(mediator);
        tasks.FindOpenAsync(Arg.Any<string>(), Arg.Any<string>(), QuestionId, Arg.Any<CancellationToken>())
            .Returns((WorkflowTask?)null);

        await consumer.Consume(Context(new CommunityOS.Contracts.Knowledge.QuestionArchived(QuestionId, Now)));

        await tasks.DidNotReceive().UpdateAsync(Arg.Any<WorkflowTask>(), Arg.Any<CancellationToken>());
    }
}