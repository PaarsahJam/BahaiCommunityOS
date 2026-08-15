using Microsoft.Extensions.Logging;

namespace CommunityOS.Documents.API.Logging;

internal static partial class DocumentsApiLog
{
    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Unhandled exception: {Message}")]
    public static partial void UnhandledException(this ILogger logger, Exception exception, string message);
}