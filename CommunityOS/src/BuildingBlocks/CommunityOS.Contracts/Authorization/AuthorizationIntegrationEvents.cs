namespace CommunityOS.Contracts.Authorization;

/// <summary>
/// Raised when a role is assigned to a subject within a scope. Consumed by
/// the future Audit service and by services that cache authorization state.
/// </summary>
public sealed record RoleAssigned(
    Guid AssignmentId,
    Guid SubjectId,
    string RoleCode,
    string ScopeType,
    Guid? ScopeId,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil,
    DateTime OccurredOn);

/// <summary>
/// Raised when a role assignment is revoked.
/// </summary>
public sealed record RoleRevoked(
    Guid AssignmentId,
    Guid SubjectId,
    string RoleCode,
    DateTime OccurredOn);

/// <summary>
/// Raised when a delegation is granted. Never contains PII.
/// </summary>
public sealed record DelegationGranted(
    Guid DelegationId,
    Guid DelegatorId,
    Guid DelegateId,
    DateTime OccurredOn);

/// <summary>
/// Raised when a delegation is revoked.
/// </summary>
public sealed record DelegationRevoked(
    Guid DelegationId,
    Guid DelegatorId,
    Guid DelegateId,
    DateTime OccurredOn);

/// <summary>
/// Raised when break-glass access is requested. High-priority audit event.
/// </summary>
public sealed record BreakGlassRequested(
    Guid RequestId,
    Guid RequesterId,
    DateTime OccurredOn);

/// <summary>
/// Raised when a break-glass request is approved and a time-limited grant
/// becomes active.
/// </summary>
public sealed record BreakGlassApproved(
    Guid RequestId,
    Guid RequesterId,
    Guid ApproverId,
    DateTime ApprovedUntil,
    DateTime OccurredOn);

/// <summary>
/// Raised when break-glass access is revoked.
/// </summary>
public sealed record BreakGlassRevoked(
    Guid RequestId,
    Guid RequesterId,
    Guid RevokedBy,
    DateTime OccurredOn);
