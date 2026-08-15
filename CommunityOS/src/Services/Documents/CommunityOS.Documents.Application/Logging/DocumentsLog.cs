using Microsoft.Extensions.Logging;

namespace CommunityOS.Documents.Application.Logging;

public static partial class DocumentsLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Document created: {DocumentId} ({Title}).")]
    public static partial void DocumentCreated(this ILogger logger, Guid documentId, string title);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Document version {VersionNumber} added to document {DocumentId} ({ContentHash}).")]
    public static partial void DocumentVersionAdded(this ILogger logger, Guid documentId, int versionNumber, string contentHash);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Unhandled exception: {Message}")]
    public static partial void UnhandledException(this ILogger logger, Exception exception, string message);
}