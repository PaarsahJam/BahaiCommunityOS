namespace CommunityOS.Identity.Domain.Exceptions;

public sealed class UserAccountNotFoundException(Guid userAccountId)
    : Exception($"User account '{userAccountId}' was not found.");

public sealed class UserAccountNotFoundByEmailException(string email)
    : Exception($"No user account exists for email '{email}'.");

public sealed class DuplicateEmailException(string email)
    : Exception($"A user account with email '{email}' already exists.");

public sealed class AccountNotVerifiedException(Guid userAccountId)
    : Exception($"User account '{userAccountId}' has not been verified.");

public sealed class AccountLockedException(Guid userAccountId, DateTime lockedUntil)
    : Exception($"User account '{userAccountId}' is locked until {lockedUntil:O}.");

public sealed class AccountDeactivatedException(Guid userAccountId)
    : Exception($"User account '{userAccountId}' has been deactivated.");

public sealed class InvalidCredentialsException()
    : Exception("The email or password is incorrect.");

public sealed class InvalidMfaCodeException()
    : Exception("The multi-factor authentication code is invalid or has expired.");

public sealed class MfaRequiredException()
    : Exception("Multi-factor authentication is required to complete sign-in.");

public sealed class InvalidSessionException()
    : Exception("The session or refresh token is invalid or has expired.");

public sealed class InvalidRecoveryTokenException()
    : Exception("The recovery token is invalid or has expired.");

public sealed class InvalidRefreshTokenException()
    : Exception("The refresh token is invalid or has expired.");

public sealed class RefreshTokenReuseDetectedException()
    : Exception("Refresh token reuse was detected; the session has been revoked.");

public sealed class UnauthorisedAccessException(string resource)
    : Exception($"Access to '{resource}' is not permitted.");

public sealed class InvalidClientException()
    : Exception("The client identifier is unknown, disabled, or the client credentials are invalid.");

public sealed class InvalidGrantException()
    : Exception("The authorization grant is invalid, expired, revoked, or does not match the request.");

public sealed class InvalidRedirectUriException(string redirectUri)
    : Exception($"The redirect URI '{redirectUri}' is not registered for this client.");

public sealed class UnauthorizedGrantException(string clientId, string grantType)
    : Exception($"Client '{clientId}' is not authorized for the '{grantType}' grant.");

public sealed class InvalidScopeException(string scope)
    : Exception($"The requested scope '{scope}' is not allowed for this client.");

public sealed class UnsupportedGrantTypeException(string grantType)
    : Exception($"The grant type '{grantType}' is not supported.");
