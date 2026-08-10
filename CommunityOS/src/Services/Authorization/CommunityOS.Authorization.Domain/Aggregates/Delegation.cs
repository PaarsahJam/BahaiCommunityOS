using CommunityOS.Authorization.Domain.Events;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Authorization.Domain.Permissions;
using CommunityOS.Authorization.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Authorization.Domain.Aggregates;

/// <summary>
/// Explicitly delegates a narrow, time-limited set of the delegator's own
/// permissions to another subject within a scope. Delegations are always
/// scoped, expiring, revocable and audited; permanent implicit delegation is
/// never permitted.
/// </summary>
public sealed class Delegation : AggregateRoot<Guid>
{
    private readonly List<string> _permissions = [];

    public Guid DelegatorId { get; private set; }
    public Guid DelegateId { get; private set; }
    public AuthorizationScope Scope { get; private set; } = null!;
    public DateTime StartsOn { get; private set; }
    public DateTime ExpiresOn { get; private set; }
    public string? Reason { get; private set; }
    public Guid? RevokedBy { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }

    public bool IsRevoked => RevokedAt is not null;
    public IReadOnlyList<string> Permissions => _permissions.AsReadOnly();

    private Delegation() : base(Guid.Empty)
    {
    }

    private Delegation(
        Guid id,
        Guid delegatorId,
        Guid delegateId,
        AuthorizationScope scope,
        IReadOnlyList<string> permissions,
        DateTime startsOn,
        DateTime expiresOn,
        string? reason) : base(id)
    {
        DelegatorId = delegatorId;
        DelegateId = delegateId;
        Scope = scope;
        StartsOn = startsOn;
        ExpiresOn = expiresOn;
        Reason = reason;

        _permissions.AddRange(
            permissions.Select(PermissionName.Normalize).Distinct(StringComparer.Ordinal));
    }

    public static Delegation Create(
        Guid delegatorId,
        Guid delegateId,
        IReadOnlyList<string> permissions,
        AuthorizationScope scope,
        DateTime startsOn,
        DateTime expiresOn,
        string? reason)
    {
        Guard.NotDefault(delegatorId, nameof(delegatorId));
        Guard.NotDefault(delegateId, nameof(delegateId));
        if (delegateId == delegatorId)
            throw new SelfDelegationException();

        Guard.NotNull(permissions, nameof(permissions));
        if (permissions.Count == 0)
            throw new ArgumentException("At least one permission is required.", nameof(permissions));
        var invalid = permissions.FirstOrDefault(p => !PermissionName.IsValid(p));
        if (invalid is not null)
            throw new InvalidPermissionException(invalid);

        Guard.NotNull(scope, nameof(scope));
        if (scope.IsGlobal)
            throw new InvalidDelegationScopeException();

        var from = startsOn == default ? DateTime.UtcNow : startsOn;
        if (expiresOn <= from)
            throw new InvalidDelegationPeriodException();

        if (reason is not null) Guard.MaxLength(reason, 500, nameof(reason));

        var delegation = new Delegation(
            Guid.NewGuid(), delegatorId, delegateId, scope, permissions, from, expiresOn, reason?.Trim());

        delegation.RaiseDomainEvent(new DelegationGrantedEvent(
            delegation.Id, delegatorId, delegateId));

        return delegation;
    }

    public bool IsActiveAt(DateTime now) =>
        !IsRevoked && now >= StartsOn && now <= ExpiresOn;

    public bool AppliesTo(AuthorizationScope targetScope) => Scope.AppliesTo(targetScope);

    public bool GrantsPermission(string permission) =>
        _permissions.Contains(permission, StringComparer.Ordinal);

    public void Revoke(Guid revokedBy, string? reason)
    {
        Guard.NotDefault(revokedBy, nameof(revokedBy));
        if (IsRevoked)
            throw new DelegationAlreadyRevokedException(Id);
        if (reason is not null) Guard.MaxLength(reason, 500, nameof(reason));

        RevokedBy = revokedBy;
        RevokedAt = DateTime.UtcNow;
        RevocationReason = reason?.Trim();

        RaiseDomainEvent(new DelegationRevokedEvent(Id, DelegatorId, DelegateId));
    }
}
