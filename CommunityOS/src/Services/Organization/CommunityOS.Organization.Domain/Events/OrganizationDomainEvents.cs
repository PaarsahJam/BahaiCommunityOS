using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Organization.Domain.Events;

public sealed record OrganizationCreatedEvent(
    Guid OrganizationId,
    string Name,
    string OrganizationType,
    string JurisdictionType,
    Guid? JurisdictionScopeId) : DomainEvent;

public sealed record OrganizationUpdatedEvent(
    Guid OrganizationId,
    string Name,
    string OrganizationType,
    string JurisdictionType,
    Guid? JurisdictionScopeId) : DomainEvent;

public sealed record OrganizationUnitCreatedEvent(
    Guid OrganizationUnitId,
    Guid OrganizationId,
    string Name,
    string UnitType,
    Guid? ParentId) : DomainEvent;

public sealed record OrganizationUnitUpdatedEvent(
    Guid OrganizationUnitId,
    string Name,
    string UnitType) : DomainEvent;

public sealed record OrganizationUnitParentChangedEvent(
    Guid OrganizationUnitId,
    Guid? ParentId,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil) : DomainEvent;

public sealed record AppointmentAssignedEvent(
    Guid AppointmentId,
    Guid PersonId,
    Guid OrganizationUnitId,
    string AppointmentType,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil) : DomainEvent;

public sealed record AppointmentEndedEvent(
    Guid AppointmentId,
    Guid PersonId,
    Guid OrganizationUnitId) : DomainEvent;

public sealed record CommitteeCreatedEvent(
    Guid CommitteeId,
    string Name,
    string CommitteeType,
    Guid OrganizationId,
    Guid? OrganizationUnitId,
    string JurisdictionType,
    Guid? JurisdictionScopeId) : DomainEvent;

public sealed record CommitteeMemberAddedEvent(
    Guid CommitteeId,
    Guid PersonId,
    string RoleCode,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil) : DomainEvent;

public sealed record CommitteeMemberRemovedEvent(
    Guid CommitteeId,
    Guid PersonId,
    string RoleCode) : DomainEvent;

public sealed record DelegationFactGrantedEvent(
    Guid DelegationFactId,
    Guid DelegatorId,
    Guid DelegateId,
    Guid OrganizationUnitId,
    string DelegationType,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil) : DomainEvent;

public sealed record DelegationFactRevokedEvent(
    Guid DelegationFactId,
    Guid DelegatorId,
    Guid DelegateId,
    Guid OrganizationUnitId) : DomainEvent;
