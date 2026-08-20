using Microsoft.Extensions.Logging;

namespace CommunityOS.Workflow.Application.Logging;

/// <summary>
/// Structured, parameterized logging for the Workflow service. Log messages
/// never include task notes, escalation reasons, names or secrets (ADR-024).
/// </summary>
public static partial class WorkflowLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Workflow task {TaskId} created (definition {DefinitionCode}).")]
    public static partial void TaskCreated(this ILogger logger, Guid taskId, string definitionCode);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Workflow task {TaskId} transitioned to {Status}.")]
    public static partial void TaskTransitioned(this ILogger logger, Guid taskId, string status);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Administrative override applied on workflow task {TaskId}: {Action}.")]
    public static partial void AdminOverrideApplied(this ILogger logger, Guid taskId, string action);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Unhandled exception: {Message}")]
    public static partial void UnhandledException(this ILogger logger, Exception exception, string message);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Task definition {Code} created.")]
    public static partial void TaskDefinitionCreated(this ILogger logger, string code);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "Task definition {Code} retired.")]
    public static partial void TaskDefinitionRetired(this ILogger logger, string code);
}