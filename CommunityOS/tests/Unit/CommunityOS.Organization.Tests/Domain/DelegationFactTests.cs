using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Enumerations;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.ValueObjects;

namespace CommunityOS.Organization.Tests.Domain;

public class DelegationFactTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static EffectivePeriod CurrentPeriod() =>
        EffectivePeriod.Create(Now.AddDays(-10), Now.AddDays(10));

    [Fact]
    public void Create_initializes_active_delegation_fact()
    {
        var fact = DelegationFact.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SigningAuthority", CurrentPeriod(), Guid.NewGuid());

        fact.Status.Should().Be(DelegationFactStatus.Active);
        fact.GrantedOn.Kind.Should().Be(DateTimeKind.Utc);
        fact.IsRevoked.Should().BeFalse();
        fact.IsActiveAt(Now).Should().BeTrue();
    }

    [Fact]
    public void Create_rejects_self_delegation()
    {
        var subject = Guid.NewGuid();

        var act = () => DelegationFact.Create(
            subject, subject, Guid.NewGuid(), "SigningAuthority", CurrentPeriod(), Guid.NewGuid());

        act.Should().Throw<SelfDelegationFactException>();
    }

    [Fact]
    public void Revoke_marks_revoked_and_records_actor()
    {
        var fact = DelegationFact.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SigningAuthority", CurrentPeriod(), Guid.NewGuid());
        var revokedBy = Guid.NewGuid();

        fact.Revoke(revokedBy, "no longer needed");

        fact.IsRevoked.Should().BeTrue();
        fact.RevokedBy.Should().Be(revokedBy);
        fact.RevokedOn.Should().NotBeNull();
        fact.IsActiveAt(Now).Should().BeFalse();
    }

    [Fact]
    public void Revoke_is_not_repeatable()
    {
        var fact = DelegationFact.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SigningAuthority", CurrentPeriod(), Guid.NewGuid());
        fact.Revoke(Guid.NewGuid(), null);

        var act = () => fact.Revoke(Guid.NewGuid(), null);

        act.Should().Throw<DelegationFactAlreadyRevokedException>();
    }

    [Fact]
    public void IsActiveAt_false_when_period_elapsed()
    {
        var fact = DelegationFact.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SigningAuthority", CurrentPeriod(), Guid.NewGuid());

        fact.IsActiveAt(Now.AddDays(11)).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_delegation_type(string? delegationType)
    {
        var act = () => DelegationFact.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), delegationType!, CurrentPeriod(), Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }
}
