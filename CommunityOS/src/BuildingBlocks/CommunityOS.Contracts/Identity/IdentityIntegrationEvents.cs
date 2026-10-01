namespace CommunityOS.Contracts.Identity;

/// <summary>
/// Raised when a new user account is registered and pending email
/// verification. Consumed by the Notifications service.
/// </summary>
public sealed record UserAccountRegistered(Guid UserAccountId, string Email, DateTime OccurredOn);

/// <summary>
/// Raised after an email address is successfully verified.
/// </summary>
public sealed record UserAccountVerified(Guid UserAccountId, DateTime OccurredOn);

/// <summary>
/// Raised when an account transitions to a locked state after repeated
/// failed authentication attempts.
/// </summary>
public sealed record UserAccountLocked(Guid UserAccountId, DateTime OccurredOn);

/// <summary>
/// Raised when a locked account is unlocked (manually or by expiry).
/// </summary>
public sealed record UserAccountUnlocked(Guid UserAccountId, DateTime OccurredOn);

/// <summary>
/// Raised when an account is deactivated.
/// </summary>
public sealed record UserAccountDeactivated(Guid UserAccountId, DateTime OccurredOn);

/// <summary>
/// Raised when the account password changes (changed or reset). May be
/// used by other services to revoke dependent tokens.
/// </summary>
public sealed record CredentialChanged(Guid UserAccountId, DateTime OccurredOn);

/// <summary>
/// Raised when an external identity (e.g. Google/Apple login) is linked
/// to an account.
/// </summary>
public sealed record ExternalIdentityLinked(Guid UserAccountId, string Provider, string Subject, DateTime OccurredOn);

/// <summary>
/// Raised when an external identity is unlinked from an account.
/// </summary>
public sealed record ExternalIdentityUnlinked(Guid UserAccountId, string Provider, string Subject, DateTime OccurredOn);

/// <summary>
/// Raised when a multi-factor authentication method is enrolled.
/// </summary>
public sealed record MfaMethodEnrolled(Guid UserAccountId, string MethodType, DateTime OccurredOn);

/// <summary>
/// Raised when a multi-factor authentication method is removed.
/// </summary>
public sealed record MfaMethodRemoved(Guid UserAccountId, string MethodType, DateTime OccurredOn);

/// <summary>
/// Raised when a new device is registered and may be used for
/// device-based authorization decisions.
/// </summary>
public sealed record DeviceRegistered(Guid UserAccountId, Guid DeviceId, string Name, DateTime OccurredOn);

/// <summary>
/// Raised when a refresh token session is issued.
/// </summary>
public sealed record RefreshTokenIssued(Guid UserAccountId, Guid SessionId, DateTime OccurredOn);

/// <summary>
/// Raised after an emergency session invalidation advances the account's
/// session-revocation epoch by exactly one. Carries the affected account and
/// the resulting epoch so downstream services can invalidate account-scoped,
/// epoch-bound state. Delivery is at least once (ADR-036 Q4): consumers must be
/// idempotent and monotonic, never reducing a locally known epoch, and
/// harmless for stale or duplicate deliveries.
/// </summary>
public sealed record SessionRevocationEpochAdvanced(Guid UserAccountId, long SessionRevocationEpoch, DateTime OccurredOn);
