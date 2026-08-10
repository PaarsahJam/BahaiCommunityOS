using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Enumerations;
using CommunityOS.Authorization.Domain.Events;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Authorization.Domain.ValueObjects;

namespace CommunityOS.Authorization.Tests.Domain;

public class DelegationTests
{
    private static readonly Guid Delegator = Guid.NewGuid();
    private static readonly Guid Delegate = Guid.NewGuid();
    private static readonly Guid Org = Guid.NewGuid();

    private static Delegation CreateDelegation(DateTime startsOn, DateTime expiresOn, IReadOnlyList<string>? permissions = null) =>
        Delegation.Create(Delegator, Delegate, permissions ?? ["records.record.read"], AuthorizationScope.Scoped(ScopeType.Local, Org), startsOn, expiresOn, "on leave");

    [Fact]
    public void Create_raises_DelegationGrantedEvent()
    {
        var now = DateTime.UtcNow;
        var delegation = CreateDelegation(now, now.AddDays(7));

        delegation.DomainEvents.Should().Contain(e => e is DelegationGrantedEvent);
        delegation.GrantsPermission("records.record.read").Should().BeTrue();
    }

    [Fact]
    public void Self_delegation_throws()
    {
        var now = DateTime.UtcNow;
        var act = () => Delegation.Create(Delegator, Delegator, ["records.record.read"], AuthorizationScope.Scoped(ScopeType.Local, Org), now, now.AddDays(1), null);

        act.Should().Throw<SelfDelegationException>();
    }

    [Fact]
    public void Global_delegation_throws()
    {
        var now = DateTime.UtcNow;
        var act = () => Delegation.Create(Delegator, Delegate, ["records.record.read"], AuthorizationScope.Global(), now, now.AddDays(1), null);

        act.Should().Throw<InvalidDelegationScopeException>();
    }

    [Fact]
    public void Expiry_before_start_throws()
    {
        var now = DateTime.UtcNow;
        var act = () => Delegation.Create(Delegator, Delegate, ["records.record.read"], AuthorizationScope.Scoped(ScopeType.Local, Org), now.AddDays(1), now, null);

        act.Should().Throw<InvalidDelegationPeriodException>();
    }

    [Fact]
    public void Empty_permissions_throw()
    {
        var now = DateTime.UtcNow;
        var act = () => Delegation.Create(Delegator, Delegate, [], AuthorizationScope.Scoped(ScopeType.Local, Org), now, now.AddDays(1), null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Invalid_permission_throws()
    {
        var now = DateTime.UtcNow;
        var act = () => Delegation.Create(Delegator, Delegate, ["invalid!"], AuthorizationScope.Scoped(ScopeType.Local, Org), now, now.AddDays(1), null);

        act.Should().Throw<InvalidPermissionException>();
    }

    [Fact]
    public void IsActiveAt_obeys_window_and_revocation()
    {
        var now = DateTime.UtcNow;
        var delegation = CreateDelegation(now.AddHours(-1), now.AddHours(1));

        delegation.IsActiveAt(now).Should().BeTrue();
        delegation.IsActiveAt(now.AddHours(2)).Should().BeFalse();

        delegation.Revoke(Delegator, "no longer needed");
        delegation.IsActiveAt(now).Should().BeFalse();
    }

    [Fact]
    public void Revoke_twice_throws()
    {
        var now = DateTime.UtcNow;
        var delegation = CreateDelegation(now, now.AddDays(1));
        delegation.Revoke(Delegator, null);

        var act = () => delegation.Revoke(Delegator, null);

        act.Should().Throw<DelegationAlreadyRevokedException>();
    }

    [Fact]
    public void Permissions_are_normalized_and_deduplicated()
    {
        var now = DateTime.UtcNow;
        var delegation = CreateDelegation(now, now.AddDays(1), ["records.record.read", "records.record.read", "records.record.update"]);

        delegation.Permissions.Should().BeEquivalentTo(["records.record.read", "records.record.update"]);
    }
}
