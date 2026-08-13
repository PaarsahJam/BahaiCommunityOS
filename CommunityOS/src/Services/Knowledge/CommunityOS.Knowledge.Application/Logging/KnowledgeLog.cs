using Microsoft.Extensions.Logging;

namespace CommunityOS.Knowledge.Application.Logging;

public static partial class KnowledgeLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Knowledge work created: {WorkId} ({Title}).")]
    public static partial void WorkCreated(this ILogger logger, Guid workId, string title);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Unhandled exception: {Message}")]
    public static partial void UnhandledException(this ILogger logger, Exception exception, string message);
}