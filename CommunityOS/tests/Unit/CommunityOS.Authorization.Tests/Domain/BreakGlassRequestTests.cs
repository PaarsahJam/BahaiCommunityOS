using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Enumerations;
using CommunityOS.Authorization.Domain.Events;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Authorization.Domain.ValueObjects;

namespace CommunityOS.Authorization.Tests.Domain;

public class BreakGlassRequestTests
{
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly Guid Approver = Guid.NewGuid();
    private static readonly Guid Org = Guid.NewGuid();

    private static BreakGlassRequest CreateRequest(TimeSpan duration, IReadOnlyList<string>? permissions = null) =>
        BreakGlassRequest.Create(Requester, AuthorizationScope.Scoped(ScopeType.Local, Org), permissions ?? ["records.record.read"], "emergency recovery", duration);

    [Fact]
    public void Create_raises_BreakGlassRequestedEvent()
    {
        var request = CreateRequest(TimeSpan.FromMinutes(30));

        request.State.Should().Be(BreakGlassRequestState.Requested);
        request.DomainEvents.Should().Contain(e => e is BreakGlassRequestedEvent);
    }

    [Fact]
    public void Global_scope_throws()
    {
        var act = () => BreakGlassRequest.Create(Requester, AuthorizationScope.Global(), ["records.record.read"], "reason", TimeSpan.FromMinutes(30));

        act.Should().Throw<BreakGlassGlobalScopeForbiddenException>();
    }

    [Fact]
    public void Non_positive_duration_throws()
    {
        var act = () => BreakGlassRequest.Create(Requester, AuthorizationScope.Scoped(ScopeType.Local, Org), ["records.record.read"], "reason", TimeSpan.Zero);

        act.Should().Throw<InvalidBreakGlassRequestException>();
    }

    [Fact]
    public void Approve_transitions_to_approved_with_window()
    {
        var request = CreateRequest(TimeSpan.FromMinutes(30));
        var now = DateTime.UtcNow;

        request.Approve(Approver, now, TimeSpan.FromMinutes(30));

        request.State.Should().Be(BreakGlassRequestState.Approved);
        request.ApproverId.Should().Be(Approver);
        request.ApprovedUntil.Should().Be(now.AddMinutes(30));
        request.IsActiveAt(now).Should().BeTrue();
        request.DomainEvents.Should().Contain(e => e is BreakGlassApprovedEvent);
    }

    [Fact]
    public void Self_approval_throws()
    {
        var request = CreateRequest(TimeSpan.FromMinutes(30));

        var act = () => request.Approve(Requester, DateTime.UtcNow, TimeSpan.FromMinutes(30));

        act.Should().Throw<SelfApprovalForbiddenException>();
    }

    [Fact]
    public void Processing_twice_throws()
    {
        var request = CreateRequest(TimeSpan.FromMinutes(30));
        request.Approve(Approver, DateTime.UtcNow, TimeSpan.FromMinutes(30));

        var act = () => request.Approve(Approver, DateTime.UtcNow, TimeSpan.FromMinutes(30));

        act.Should().Throw<BreakGlassAlreadyProcessedException>();
    }

    [Fact]
    public void Reject_transitions_to_rejected()
    {
        var request = CreateRequest(TimeSpan.FromMinutes(30));

        request.Reject(Approver, "not an emergency");

        request.State.Should().Be(BreakGlassRequestState.Rejected);
        request.RejectionReason.Should().Be("not an emergency");
        request.DomainEvents.Should().Contain(e => e is BreakGlassRejectedEvent);
    }

    [Fact]
    public void Revoke_after_approval_removes_access()
    {
        var request = CreateRequest(TimeSpan.FromMinutes(30));
        request.Approve(Approver, DateTime.UtcNow, TimeSpan.FromMinutes(30));

        request.Revoke(Approver, "resolved early");

        request.State.Should().Be(BreakGlassRequestState.Revoked);
        request.IsActiveAt(DateTime.UtcNow).Should().BeFalse();
        request.DomainEvents.Should().Contain(e => e is BreakGlassRevokedEvent);
    }

    [Fact]
    public void IsActiveAt_is_false_after_window_elapses()
    {
        var request = CreateRequest(TimeSpan.FromMinutes(30));
        var now = DateTime.UtcNow;
        request.Approve(Approver, now.AddHours(-1), TimeSpan.FromMinutes(30));

        request.IsActiveAt(now).Should().BeFalse();
    }

    [Fact]
    public void ExpireIfNeeded_marks_expired_after_window()
    {
        var request = CreateRequest(TimeSpan.FromMinutes(30));
        var now = DateTime.UtcNow;
        request.Approve(Approver, now.AddHours(-1), TimeSpan.FromMinutes(30));

        request.ExpireIfNeeded(now);

        request.State.Should().Be(BreakGlassRequestState.Expired);
    }

    [Fact]
    public void ExpireIfNeeded_does_nothing_within_window()
    {
        var request = CreateRequest(TimeSpan.FromMinutes(30));
        var now = DateTime.UtcNow;
        request.Approve(Approver, now, TimeSpan.FromMinutes(30));

        request.ExpireIfNeeded(now);

        request.State.Should().Be(BreakGlassRequestState.Approved);
    }
}
