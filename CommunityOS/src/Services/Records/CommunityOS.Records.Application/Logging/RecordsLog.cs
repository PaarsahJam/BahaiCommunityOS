using Microsoft.Extensions.Logging;

namespace CommunityOS.Records.Application.Logging;

/// <summary>
/// Structured, parameterized logging for the Records service. Log messages
/// never include field values, hold reasons, secrets or names (ADR-023).
/// </summary>
public static partial class RecordsLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Record created: {RecordId} (category {Category}).")]
    public static partial void RecordCreated(this ILogger logger, Guid recordId, string category);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Record {RecordId} transitioned to {Status}.")]
    public static partial void RecordTransitioned(this ILogger logger, Guid recordId, string status);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Administrative override applied on record {RecordId}: {Action}.")]
    public static partial void AdminOverrideApplied(this ILogger logger, Guid recordId, string action);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Unhandled exception: {Message}")]
    public static partial void UnhandledException(this ILogger logger, Exception exception, string message);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "Documents service command failed for record {RecordId} (document {DocumentId}): {Action}.")]
    public static partial void DocumentsCommandFailed(
        this ILogger logger, Guid recordId, Guid documentId, string action, Exception exception);
}