using CommunityOS.Authorization.Domain.Events;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Authorization.Domain.Enumerations;
using CommunityOS.Authorization.Domain.Permissions;
using CommunityOS.Authorization.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Authorization.Domain.Aggregates;

/// <summary>
/// A controlled, time-limited emergency access grant. Break-glass requires
/// explicit invocation, a reason, a narrow scope, approval by a different
/// subject, and generates high-priority audit events. It never silently
/// bypasses normal authorization or grants global access.
/// </summary>
public sealed class BreakGlassRequest : AggregateRoot<Guid>
{
    private readonly List<string> _permissions = [];

    public Guid RequesterId { get; private set; }
    public AuthorizationScope Scope { get; private set; } = null!;
    public string Reason { get; private set; } = null!;
    public DateTime RequestedAt { get; private set; }
    public TimeSpan RequestedDuration { get; private set; }
    public BreakGlassRequestState State { get; private set; } = null!;
    public Guid? ApproverId { get; private set; }
    public DateTime? ApprovedOn { get; private set; }
    public DateTime? ApprovedUntil { get; private set; }
    public string? RejectionReason { get; private set; }
    public Guid? RevokedBy { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }

    public IReadOnlyList<string> Permissions => _permissions.AsReadOnly();

    private BreakGlassRequest() : base(Guid.Empty)
    {
    }

    private BreakGlassRequest(
        Guid id,
        Guid requesterId,
        AuthorizationScope scope,
        string reason,
        DateTime requestedAt,
        TimeSpan requestedDuration) : base(id)
    {
        RequesterId = requesterId;
        Scope = scope;
        Reason = reason;
        RequestedAt = requestedAt;
        RequestedDuration = requestedDuration;
        State = BreakGlassRequestState.Requested;
    }

    public static BreakGlassRequest Create(
        Guid requesterId,
        AuthorizationScope scope,
        IReadOnlyList<string> permissions,
        string reason,
        TimeSpan requestedDuration,
        DateTime? requestedAt = null)
    {
        Guard.NotDefault(requesterId, nameof(requesterId));
        Guard.NotNull(scope, nameof(scope));
        if (scope.IsGlobal)
            throw new BreakGlassGlobalScopeForbiddenException();

        Guard.NotNullOrWhiteSpace(reason, nameof(reason));
        Guard.MaxLength(reason, 1000, nameof(reason));

        Guard.NotNull(permissions, nameof(permissions));
        if (permissions.Count == 0)
            throw new ArgumentException("At least one permission is required.", nameof(permissions));
        var invalid = permissions.FirstOrDefault(p => !PermissionName.IsValid(p));
        if (invalid is not null)
            throw new InvalidPermissionException(invalid);

        if (requestedDuration <= TimeSpan.Zero)
            throw new InvalidBreakGlassRequestException("Break-glass duration must be positive.");

        var at = requestedAt ?? DateTime.UtcNow;
        var request = new BreakGlassRequest(
            Guid.NewGuid(), requesterId, scope, reason.Trim(), at, requestedDuration);

        request._permissions.AddRange(
            permissions.Select(PermissionName.Normalize).Distinct(StringComparer.Ordinal));

        request.RaiseDomainEvent(new BreakGlassRequestedEvent(request.Id, requesterId));
        return request;
    }

    public void Approve(Guid approverId, DateTime approvedOn, TimeSpan duration)
    {
        Guard.NotDefault(approverId, nameof(approverId));
        if (approverId == RequesterId)
            throw new SelfApprovalForbiddenException();
        if (State != BreakGlassRequestState.Requested)
            throw new BreakGlassAlreadyProcessedException(Id);
        if (duration <= TimeSpan.Zero)
            throw new InvalidBreakGlassRequestException("Approved break-glass duration must be positive.");

        ApproverId = approverId;
        ApprovedOn = approvedOn;
        ApprovedUntil = approvedOn.Add(duration);
        State = BreakGlassRequestState.Approved;

        RaiseDomainEvent(new BreakGlassApprovedEvent(Id, RequesterId, approverId, ApprovedUntil.Value));
    }

    public void Reject(Guid approverId, string? reason)
    {
        Guard.NotDefault(approverId, nameof(approverId));
        if (State != BreakGlassRequestState.Requested)
            throw new BreakGlassAlreadyProcessedException(Id);
        if (reason is not null) Guard.MaxLength(reason, 1000, nameof(reason));

        ApproverId = approverId;
        RejectionReason = reason?.Trim();
        State = BreakGlassRequestState.Rejected;

        RaiseDomainEvent(new BreakGlassRejectedEvent(Id, RequesterId, approverId));
    }

    public void Revoke(Guid revokedBy, string? reason)
    {
        Guard.NotDefault(revokedBy, nameof(revokedBy));
        if (State == BreakGlassRequestState.Revoked ||
            State == BreakGlassRequestState.Expired ||
            State == BreakGlassRequestState.Rejected)
            throw new BreakGlassAlreadyProcessedException(Id);
        if (reason is not null) Guard.MaxLength(reason, 1000, nameof(reason));

        RevokedBy = revokedBy;
        RevokedAt = DateTime.UtcNow;
        RevocationReason = reason?.Trim();
        State = BreakGlassRequestState.Revoked;

        RaiseDomainEvent(new BreakGlassRevokedEvent(Id, RequesterId, revokedBy));
    }

    public bool IsActiveAt(DateTime now) =>
        State == BreakGlassRequestState.Approved &&
        ApprovedOn is not null &&
        ApprovedUntil is not null &&
        now >= ApprovedOn &&
        now <= ApprovedUntil;

    public void ExpireIfNeeded(DateTime now)
    {
        if (State == BreakGlassRequestState.Approved && ApprovedUntil is { } until && now > until)
            State = BreakGlassRequestState.Expired;
    }

    public bool AppliesTo(AuthorizationScope targetScope) => Scope.AppliesTo(targetScope);

    public bool GrantsPermission(string permission) =>
        _permissions.Contains(permission, StringComparer.Ordinal);
}
