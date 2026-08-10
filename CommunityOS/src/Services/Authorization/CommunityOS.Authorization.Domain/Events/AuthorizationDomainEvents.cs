using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Authorization.Domain.Events;

public sealed record RoleCreatedEvent(Guid RoleId, string Code) : DomainEvent;

public sealed record RolePermissionsChangedEvent(Guid RoleId, string Code) : DomainEvent;

public sealed record RoleAssignedEvent(
    Guid AssignmentId,
    Guid SubjectId,
    string RoleCode,
    string ScopeType,
    Guid? ScopeId,
    string? ResourceType,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil) : DomainEvent;

public sealed record RoleRevokedEvent(Guid AssignmentId, Guid SubjectId, string RoleCode) : DomainEvent;

public sealed record RelationshipWrittenEvent(
    Guid RelationshipId, Guid SubjectId, string Relation, string ObjectType, Guid ObjectId) : DomainEvent;

public sealed record DelegationGrantedEvent(Guid DelegationId, Guid DelegatorId, Guid DelegateId) : DomainEvent;

public sealed record DelegationRevokedEvent(Guid DelegationId, Guid DelegatorId, Guid DelegateId) : DomainEvent;

public sealed record BreakGlassRequestedEvent(Guid RequestId, Guid RequesterId) : DomainEvent;

public sealed record BreakGlassApprovedEvent(
    Guid RequestId, Guid RequesterId, Guid ApproverId, DateTime ApprovedUntil) : DomainEvent;

public sealed record BreakGlassRejectedEvent(Guid RequestId, Guid RequesterId, Guid ApproverId) : DomainEvent;

public sealed record BreakGlassRevokedEvent(Guid RequestId, Guid RequesterId, Guid RevokedBy) : DomainEvent;
