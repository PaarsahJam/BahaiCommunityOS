using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Events;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.ValueObjects;

namespace CommunityOS.Community.Tests.Domain;

public class MembershipTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public void Create_initializes_membership_with_history()
    {
        var personId = Guid.NewGuid();

        var membership = Membership.Create(personId, MembershipStatus.Active, EffectivePeriod.Create(Now));

        membership.Id.Should().NotBeEmpty();
        membership.PersonId.Should().Be(personId);
        membership.Status.Should().Be(MembershipStatus.Active);
        membership.PeriodHistory.Should().ContainSingle();
        membership.DomainEvents.Should().ContainSingle(e => e is MembershipChangedEvent);
    }

    [Fact]
    public void IsEffectiveAt_checks_status_and_window()
    {
        var membership = Membership.Create(
            Guid.NewGuid(), MembershipStatus.Active, EffectivePeriod.Create(Now.AddDays(-10), Now.AddDays(10)));

        membership.IsEffectiveAt(MembershipStatus.Active, Now).Should().BeTrue();
        membership.IsEffectiveAt(MembershipStatus.Suspended, Now).Should().BeFalse();
        membership.IsEffectiveAt(MembershipStatus.Active, Now.AddDays(20)).Should().BeFalse();
    }

    [Fact]
    public void ChangeStatus_appends_history_and_updates_window()
    {
        var membership = Membership.Create(
            Guid.NewGuid(), MembershipStatus.Active, EffectivePeriod.Create(Now.AddDays(-10)));
        var changedOn = Now;

        membership.ChangeStatus(MembershipStatus.Withdrawn, changedOn);

        membership.Status.Should().Be(MembershipStatus.Withdrawn);
        membership.WithdrawnOn.Should().Be(changedOn);
        membership.PeriodHistory.Should().HaveCount(2);
        membership.DomainEvents.Should().HaveCount(2);
    }

    [Fact]
    public void ChangeStatus_rejects_unchanged_status()
    {
        var membership = Membership.Create(
            Guid.NewGuid(), MembershipStatus.Active, EffectivePeriod.Create(Now));

        var act = () => membership.ChangeStatus(MembershipStatus.Active);

        act.Should().Throw<MembershipStatusUnchangedException>();
    }

    [Fact]
    public void ChangeStatus_rejects_effective_from_before_current_window()
    {
        var membership = Membership.Create(
            Guid.NewGuid(), MembershipStatus.Active, EffectivePeriod.Create(Now));

        var act = () => membership.ChangeStatus(MembershipStatus.Suspended, Now.AddDays(-1));

        act.Should().Throw<InvalidEffectivePeriodException>();
    }
}
