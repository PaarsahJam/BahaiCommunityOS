using CommunityOS.Contracts.Workflow;
using CommunityOS.Notifications.Domain.Repositories;
using CommunityOS.Notifications.Infrastructure.Integration.Workflow;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CommunityOS.Notifications.Tests.Integration;

/// <summary>
/// Workflow reconcile consumers (ADR-024 → ADR-025, first gate):
/// <c>WorkflowTaskAssigned</c> creates a <c>task-assigned</c> notification to
/// each assignee; <c>WorkflowTaskEscalated</c> creates a <c>task-escalated</c>
/// notification to each escalation target. Reconciliation is create-if-absent
/// and idempotent under duplicates, and skips events without recipients.
/// </summary>
public class WorkflowTaskIntegrationEventConsumerTests
{
    private static readonly Guid TaskId = Guid.NewGuid();
    private static readonly Guid Assignee = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static ConsumeContext<T> Context<T>(T message)
        where T : class
    {
        var ctx = Substitute.For<ConsumeContext<T>>();
        ctx.Message.Returns(message);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    private static (
        WorkflowTaskIntegrationEventConsumer Consumer,
        INotificationRepository Notifications,
        INotificationTypeRepository Types) Build()
    {
        var notifications = Substitute.For<INotificationRepository>();
        var types = Substitute.For<INotificationTypeRepository>();
        var mediator = Substitute.For<IMediator>();
        var consumer = new WorkflowTaskIntegrationEventConsumer(
            notifications, types, mediator, NullLogger<WorkflowTaskIntegrationEventConsumer>.Instance);
        return (consumer, notifications, types);
    }

    [Fact]
    public async Task TaskAssigned_creates_a_queued_notification_for_each_assignee()
    {
        var (consumer, notifications, types) = Build();
        notifications.GetBySourceAsync(
                NotificationTypeCodes.TaskAssigned, "workflow-task", TaskId, Arg.Any<CancellationToken>())
            .Returns((Notification?)null);
        notifications.AddIfAbsentAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Notification>());

        await consumer.Consume(Context(new WorkflowTaskAssigned(
            TaskId, "record-review", [Assignee], Actor, Now)));

        await notifications.Received(1).AddIfAbsentAsync(
            Arg.Is<Notification>(n =>
                n.TypeCode == NotificationTypeCodes.TaskAssigned &&
                n.SourceType == "workflow-task" &&
                n.SourceId == TaskId &&
                n.Recipients.Count == 1 &&
                n.Recipients.Single().MemberId == Assignee),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TaskAssigned_is_idempotent_under_duplicates()
    {
        var (consumer, notifications, types) = Build();
        notifications.GetBySourceAsync(
                NotificationTypeCodes.TaskAssigned, "workflow-task", TaskId, Arg.Any<CancellationToken>())
            .Returns(Notification.Create(
                NotificationTypeCodes.TaskAssigned,
                CommunityOS.Notifications.Domain.ValueObjects.NotificationChannel.InApp,
                "workflow-task",
                TaskId,
                CommunityOS.Notifications.Domain.ValueObjects.MessageTemplate.Create("s", "b"),
                null, [], null, false, [Assignee], Guid.NewGuid(), Now));

        await consumer.Consume(Context(new WorkflowTaskAssigned(
            TaskId, "record-review", [Assignee], Actor, Now)));

        await notifications.DidNotReceive().AddIfAbsentAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TaskAssigned_without_assignees_skips_creation()
    {
        var (consumer, notifications, types) = Build();

        await consumer.Consume(Context(new WorkflowTaskAssigned(
            TaskId, "record-review", [], Actor, Now)));

        await notifications.DidNotReceiveWithAnyArgs().AddIfAbsentAsync(default!, default);
    }

    [Fact]
    public async Task TaskEscalated_creates_a_notification_for_each_target()
    {
        var (consumer, notifications, types) = Build();
        notifications.AddIfAbsentAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Notification>());

        await consumer.Consume(Context(new WorkflowTaskEscalated(
            TaskId, "record-review", [Assignee], Actor, Now)));

        await notifications.Received(1).AddIfAbsentAsync(
            Arg.Is<Notification>(n =>
                n.TypeCode == NotificationTypeCodes.TaskEscalated &&
                n.Recipients.Single().MemberId == Assignee),
            Arg.Any<CancellationToken>());
    }
}