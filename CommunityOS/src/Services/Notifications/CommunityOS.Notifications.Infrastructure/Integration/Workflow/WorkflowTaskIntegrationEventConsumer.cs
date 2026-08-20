using CommunityOS.Contracts.Workflow;
using CommunityOS.Notifications.Domain.Repositories;
using CommunityOS.Notifications.Domain.ValueObjects;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using static CommunityOS.Notifications.Infrastructure.Integration.NotificationsReconciliation;

namespace CommunityOS.Notifications.Infrastructure.Integration.Workflow;

/// <summary>
/// Creates task-routing digests from Workflow integration events (ADR-024 →
/// ADR-025, first gate). <c>WorkflowTaskAssigned</c> creates a
/// <c>task-assigned</c> notification to each <c>AssigneeId</c>;
/// <c>WorkflowTaskEscalated</c> creates a <c>task-escalated</c> notification to
/// each <c>EscalatedTo</c>. Notifications never creates, assigns, completes or
/// cancels tasks; Workflow is the authoritative source of task state.
/// <c>WorkflowTaskCreated</c> is not a trigger (a task is not assigned until
/// <c>WorkflowTaskAssigned</c>). Reconciliation is create-if-absent and
/// idempotent under duplicates (per (type, workflow-task, task id, InApp)).
/// </summary>
public sealed class WorkflowTaskIntegrationEventConsumer(
    INotificationRepository notifications,
    INotificationTypeRepository types,
    IMediator mediator,
    ILogger<WorkflowTaskIntegrationEventConsumer> logger) :
    IConsumer<WorkflowTaskAssigned>,
    IConsumer<WorkflowTaskEscalated>
{
    public async Task Consume(ConsumeContext<WorkflowTaskAssigned> context)
    {
        var message = context.Message;
        var notification = await CreateIfAbsentAsync(
            notifications, types, mediator,
            NotificationTypeCodes.TaskAssigned,
            WorkflowTaskSourceType,
            message.TaskId,
            message.AssigneeIds,
            TaskVariables(message.TaskId),
            message.OccurredOn,
            context.CancellationToken);

        if (notification is not null)
            logger.TaskAssignedNotificationCreated(notification.Id, message.TaskId);
    }

    public async Task Consume(ConsumeContext<WorkflowTaskEscalated> context)
    {
        var message = context.Message;
        var notification = await CreateIfAbsentAsync(
            notifications, types, mediator,
            NotificationTypeCodes.TaskEscalated,
            WorkflowTaskSourceType,
            message.TaskId,
            message.EscalatedTo,
            TaskVariables(message.TaskId),
            message.OccurredOn,
            context.CancellationToken);

        if (notification is not null)
            logger.TaskEscalatedNotificationCreated(notification.Id, message.TaskId);
    }

    private static Dictionary<string, string> TaskVariables(Guid taskId) =>
        new() { ["TaskId"] = taskId.ToString("D") };
}

/// <summary>Baseline notification-type codes referenced by the reconcile consumers (ADR-025).</summary>
public static class NotificationTypeCodes
{
    public const string TaskAssigned = "task-assigned";
    public const string TaskEscalated = "task-escalated";
    public const string TaskCompleted = "task-completed";
    public const string TaskCancelled = "task-cancelled";
    public const string RecordVerified = "record-verified";
    public const string RecordRejected = "record-rejected";
    public const string RecordHold = "record-hold";
    public const string QuestionFlagged = "question-flagged";
    public const string CommunityActivity = "community-activity";
    public const string CommunityEvent = "community-event";
    public const string CommunityMeeting = "community-meeting";
    public const string General = "general";
}

internal static partial class WorkflowTaskIntegrationEventConsumerLogging
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Created task-assigned notification {NotificationId} for task {TaskId}.")]
    public static partial void TaskAssignedNotificationCreated(this ILogger logger, Guid notificationId, Guid taskId);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Created task-escalated notification {NotificationId} for task {TaskId}.")]
    public static partial void TaskEscalatedNotificationCreated(this ILogger logger, Guid notificationId, Guid taskId);
}