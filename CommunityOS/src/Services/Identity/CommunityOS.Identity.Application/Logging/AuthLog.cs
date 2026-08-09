using Microsoft.Extensions.Logging;

namespace CommunityOS.Identity.Application.Logging;

public static partial class AuthLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Sign-in succeeded for user {UserAccountId}.")]
    public static partial void SignInSucceeded(this ILogger logger, Guid userAccountId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Refresh token reuse detected for user {UserAccountId}; family revoked.")]
    public static partial void RefreshTokenReuseDetected(this ILogger logger, Guid userAccountId);
}
