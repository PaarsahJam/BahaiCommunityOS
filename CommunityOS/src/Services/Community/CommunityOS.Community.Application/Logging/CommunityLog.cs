using Microsoft.Extensions.Logging;

namespace CommunityOS.Community.Application.Logging;

public static partial class CommunityLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Community created: {CommunityId} ({Name}).")]
    public static partial void CommunityCreated(this ILogger logger, Guid communityId, string name);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Unhandled exception: {Message}")]
    public static partial void UnhandledException(this ILogger logger, Exception exception, string message);
}
