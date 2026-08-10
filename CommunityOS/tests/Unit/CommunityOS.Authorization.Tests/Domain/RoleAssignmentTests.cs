using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Enumerations;
using CommunityOS.Authorization.Domain.Events;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Authorization.Domain.ValueObjects;

namespace CommunityOS.Authorization.Tests.Domain;

public class RoleAssignmentTests
{
    private static readonly Guid Subject = Guid.NewGuid();
    private static readonly Guid RoleId = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid Org = Guid.NewGuid();

    private static RoleAssignment CreateAssignment(DateTime? from = null, DateTime? until = null) =>
        RoleAssignment.Create(Subject, RoleId, "editor", AuthorizationScope.Scoped(ScopeType.Local, Org), Actor, DateTime.UtcNow, from, until, null);

    [Fact]
    public void Create_raises_enriched_RoleAssignedEvent()
    {
        var assignment = CreateAssignment();

        var evt = assignment.DomainEvents.Should().ContainSingle(e => e is RoleAssignedEvent).Which as RoleAssignedEvent;
        evt!.SubjectId.Should().Be(Subject);
        evt.RoleCode.Should().Be("editor");
        evt.ScopeType.Should().Be("Local");
        evt.ScopeId.Should().Be(Org);
        evt.ResourceType.Should().BeNull();
        evt.EffectiveFrom.Should().Be(assignment.EffectiveFrom);
        evt.EffectiveUntil.Should().Be(assignment.EffectiveUntil);
    }

    [Fact]
    public void EffectiveFrom_defaults_to_grantedAt_when_not_supplied()
    {
        var assignment = RoleAssignment.Create(Subject, RoleId, "editor", AuthorizationScope.Global(), Actor, grantedAt: DateTime.UtcNow, null, null, null);

        assignment.EffectiveFrom.Should().Be(assignment.GrantedAt);
    }

    [Fact]
    public void EffectiveUntil_before_EffectiveFrom_throws()
    {
        var now = DateTime.UtcNow;
        var act = () => RoleAssignment.Create(Subject, RoleId, "editor", AuthorizationScope.Global(), Actor, now, now, now.AddMinutes(-5), null);

        act.Should().Throw<InvalidEffectiveRangeException>();
    }

    [Fact]
    public void IsEffectiveAt_obeys_effective_window_and_revocation()
    {
        var now = DateTime.UtcNow;
        var assignment = CreateAssignment(from: now.AddHours(-2), until: now.AddHours(2));

        assignment.IsEffectiveAt(now).Should().BeTrue();
        assignment.IsEffectiveAt(now.AddHours(3)).Should().BeFalse();

        assignment.Revoke(Actor, "restructure");
        assignment.IsEffectiveAt(now).Should().BeFalse();
    }

    [Fact]
    public void IsEffectiveAt_is_false_outside_future_window()
    {
        var now = DateTime.UtcNow;
        var assignment = CreateAssignment(from: now.AddHours(1), until: now.AddHours(2));

        assignment.IsEffectiveAt(now).Should().BeFalse();
    }

    [Fact]
    public void Revoke_marks_revoked_and_raises_event()
    {
        var assignment = CreateAssignment();

        assignment.Revoke(Actor, "reorganized");

        assignment.IsRevoked.Should().BeTrue();
        assignment.RevokedBy.Should().Be(Actor);
        assignment.RevocationReason.Should().Be("reorganized");
        assignment.DomainEvents.Should().Contain(e => e is RoleRevokedEvent);
    }

    [Fact]
    public void Revoke_twice_throws()
    {
        var assignment = CreateAssignment();
        assignment.Revoke(Actor, null);

        var act = () => assignment.Revoke(Actor, null);

        act.Should().Throw<RoleAssignmentAlreadyRevokedException>();
    }

    [Fact]
    public void AppliesTo_delegates_to_scope()
    {
        var assignment = CreateAssignment();
        assignment.AppliesTo(AuthorizationScope.Scoped(ScopeType.Local, Org)).Should().BeTrue();
        assignment.AppliesTo(AuthorizationScope.Scoped(ScopeType.National, Org)).Should().BeFalse();
    }

    [Fact]
    public void Create_rejects_default_subject_and_grantedBy()
    {
        var act = () => RoleAssignment.Create(Guid.Empty, RoleId, "editor", AuthorizationScope.Global(), Actor, DateTime.UtcNow, null, null, null);
        act.Should().Throw<ArgumentException>();

        var actActor = () => RoleAssignment.Create(Subject, RoleId, "editor", AuthorizationScope.Global(), Guid.Empty, DateTime.UtcNow, null, null, null);
        actActor.Should().Throw<ArgumentException>();
    }
}
