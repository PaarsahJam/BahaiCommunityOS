using CommunityOS.Authorization.Domain.Events;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Authorization.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Authorization.Domain.Aggregates;

/// <summary>
/// Grants a role to a subject within a scope. Assignments are auditable,
/// scoped, revocable and effective-dated. A role granted globally does NOT
/// automatically grant access to every domain resource.
/// </summary>
public sealed class RoleAssignment : AggregateRoot<Guid>
{
    public Guid SubjectId { get; private set; }
    public Guid RoleId { get; private set; }
    public string RoleCode { get; private set; } = null!;
    public AuthorizationScope Scope { get; private set; } = null!;
    public Guid GrantedBy { get; private set; }
    public DateTime GrantedAt { get; private set; }
    public DateTime EffectiveFrom { get; private set; }
    public DateTime? EffectiveUntil { get; private set; }
    public string? Reason { get; private set; }
    public Guid? RevokedBy { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    private RoleAssignment() : base(Guid.Empty)
    {
    }

    private RoleAssignment(
        Guid id,
        Guid subjectId,
        Guid roleId,
        string roleCode,
        AuthorizationScope scope,
        Guid grantedBy,
        DateTime grantedAt,
        DateTime effectiveFrom,
        DateTime? effectiveUntil,
        string? reason) : base(id)
    {
        SubjectId = subjectId;
        RoleId = roleId;
        RoleCode = roleCode;
        Scope = scope;
        GrantedBy = grantedBy;
        GrantedAt = grantedAt;
        EffectiveFrom = effectiveFrom;
        EffectiveUntil = effectiveUntil;
        Reason = reason;
    }

    public static RoleAssignment Create(
        Guid subjectId,
        Guid roleId,
        string roleCode,
        AuthorizationScope scope,
        Guid grantedBy,
        DateTime grantedAt,
        DateTime? effectiveFrom,
        DateTime? effectiveUntil,
        string? reason)
    {
        Guard.NotDefault(subjectId, nameof(subjectId));
        Guard.NotDefault(roleId, nameof(roleId));
        Guard.NotNullOrWhiteSpace(roleCode, nameof(roleCode));
        Guard.NotNull(scope, nameof(scope));
        Guard.NotDefault(grantedBy, nameof(grantedBy));
        if (reason is not null) Guard.MaxLength(reason, 500, nameof(reason));

        var from = effectiveFrom ?? grantedAt;
        if (effectiveUntil is { } until && until < from)
            throw new InvalidEffectiveRangeException();

        var normalized = roleCode.Trim();
        var assignment = new RoleAssignment(
            Guid.NewGuid(), subjectId, roleId, normalized, scope, grantedBy, grantedAt, from, effectiveUntil, reason?.Trim());

        assignment.RaiseDomainEvent(new RoleAssignedEvent(
            assignment.Id,
            subjectId,
            normalized,
            assignment.Scope.Type.Name,
            assignment.Scope.ScopeId,
            assignment.Scope.ResourceType,
            assignment.EffectiveFrom,
            assignment.EffectiveUntil));
        return assignment;
    }

    public bool IsEffectiveAt(DateTime now) =>
        !IsRevoked && now >= EffectiveFrom && (EffectiveUntil is null || now <= EffectiveUntil);

    public bool AppliesTo(AuthorizationScope targetScope) => Scope.AppliesTo(targetScope);

    public void Revoke(Guid revokedBy, string? reason)
    {
        Guard.NotDefault(revokedBy, nameof(revokedBy));
        if (IsRevoked)
            throw new RoleAssignmentAlreadyRevokedException(Id);
        if (reason is not null) Guard.MaxLength(reason, 500, nameof(reason));

        RevokedBy = revokedBy;
        RevokedAt = DateTime.UtcNow;
        RevocationReason = reason?.Trim();

        RaiseDomainEvent(new RoleRevokedEvent(Id, SubjectId, RoleCode));
    }
}
