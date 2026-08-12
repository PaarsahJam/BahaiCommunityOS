namespace CommunityOS.Contracts.Organization;

/// <summary>
/// Raised when an organization is created. An organization is a Faith
/// institution (e.g. a National Spiritual Assembly, a Local Spiritual Assembly)
/// that owns organization units, hierarchy and appointments.
/// </summary>
public sealed record OrganizationCreated(
    Guid OrganizationId,
    string Name,
    string OrganizationType,
    string JurisdictionType,
    Guid? JurisdictionScopeId,
    DateTime OccurredOn);

/// <summary>
/// Raised when an organization's details or jurisdiction change.
/// </summary>
public sealed record OrganizationUpdated(
    Guid OrganizationId,
    string Name,
    string OrganizationType,
    string JurisdictionType,
    Guid? JurisdictionScopeId,
    DateTime OccurredOn);

/// <summary>
/// Raised when an organization unit is created. Organization units are the
/// nodes of the organizational hierarchy.
/// </summary>
public sealed record OrganizationUnitCreated(
    Guid OrganizationUnitId,
    Guid OrganizationId,
    string Name,
    string UnitType,
    Guid? ParentId,
    DateTime OccurredOn);

/// <summary>
/// Raised when an organization unit's details change.
/// </summary>
public sealed record OrganizationUnitUpdated(
    Guid OrganizationUnitId,
    string Name,
    string UnitType,
    DateTime OccurredOn);

/// <summary>
/// Raised when an organization unit's effective parent changes. Carries the
/// new effective window so consumers can reconstruct hierarchy history.
/// </summary>
public sealed record OrganizationUnitParentChanged(
    Guid OrganizationUnitId,
    Guid? ParentId,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil,
    DateTime OccurredOn);

/// <summary>
/// Raised when a person is appointed to a role within an organization unit.
/// Person identity is owned by the Community service; the Organization service
/// only carries the stable person id.
/// </summary>
public sealed record AppointmentAssigned(
    Guid AppointmentId,
    Guid PersonId,
    Guid OrganizationUnitId,
    string AppointmentType,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil,
    DateTime OccurredOn);

/// <summary>
/// Raised when an appointment is ended (revoked or completed).
/// </summary>
public sealed record AppointmentEnded(
    Guid AppointmentId,
    Guid PersonId,
    Guid OrganizationUnitId,
    DateTime OccurredOn);

/// <summary>
/// Raised when a committee is created.
/// </summary>
public sealed record CommitteeCreated(
    Guid CommitteeId,
    string Name,
    string CommitteeType,
    Guid OrganizationId,
    Guid? OrganizationUnitId,
    string JurisdictionType,
    Guid? JurisdictionScopeId,
    DateTime OccurredOn);

/// <summary>
/// Raised when a committee member is added.
/// </summary>
public sealed record CommitteeMemberAdded(
    Guid CommitteeId,
    Guid PersonId,
    string RoleCode,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil,
    DateTime OccurredOn);

/// <summary>
/// Raised when a committee member is removed.
/// </summary>
public sealed record CommitteeMemberRemoved(
    Guid CommitteeId,
    Guid PersonId,
    string RoleCode,
    DateTime OccurredOn);

/// <summary>
/// Raised when a delegation fact (authority delegated from one subject to
/// another) is granted.
/// </summary>
public sealed record DelegationFactGranted(
    Guid DelegationFactId,
    Guid DelegatorId,
    Guid DelegateId,
    Guid OrganizationUnitId,
    string DelegationType,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil,
    DateTime OccurredOn);

/// <summary>
/// Raised when a delegation fact is revoked.
/// </summary>
public sealed record DelegationFactRevoked(
    Guid DelegationFactId,
    Guid DelegatorId,
    Guid DelegateId,
    Guid OrganizationUnitId,
    DateTime OccurredOn);
