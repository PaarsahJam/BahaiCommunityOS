using Microsoft.Extensions.Logging;

namespace CommunityOS.Records.API.Logging;

internal static partial class RecordsApiLog
{
    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Unhandled exception: {Message}")]
    public static partial void UnhandledException(this ILogger logger, Exception exception, string message);
}