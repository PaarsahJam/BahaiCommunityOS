namespace CommunityOS.Identity.Domain.Exceptions;

public sealed class InvalidCredentialsException()
    : Exception("The email or password is incorrect.");

public sealed class MemberNotActiveException(Guid memberId)
    : Exception($"Member '{memberId}' is not active.");

public sealed class InvalidRefreshTokenException()
    : Exception("The refresh token is invalid or has expired.");

public sealed class UnauthorisedAccessException(string resource)
    : Exception($"Access to '{resource}' is not permitted.");
