using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Events;
using CommunityOS.Identity.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Events;
using FluentAssertions;

namespace CommunityOS.Identity.Tests.Domain;

/// <summary>
/// ADR-036 D1: the session-revocation epoch starts at zero, advances by exactly
/// one per invocation, is strictly monotonic (never decremented or reset), and
/// each advance raises exactly one <see cref="SessionRevocationEpochAdvancedEvent"/>.
/// </summary>
public sealed class UserAccountSessionRevocationTests
{
    private static UserAccount NewAccount() =>
        UserAccount.Register(Email.Create($"epoch-{Guid.NewGuid():N}@example.org"), "password-hash");

    [Fact]
    public void NewAccount_StartsWithEpochZero()
    {
        var account = NewAccount();

        account.SessionRevocationEpoch.Should().Be(0);
    }

    [Fact]
    public void SingleAdvance_IsExactlyOne()
    {
        var account = NewAccount();

        account.AdvanceSessionRevocationEpochOnce();

        account.SessionRevocationEpoch.Should().Be(1);
    }

    [Fact]
    public void RepeatedAdvances_AreExactlyOneAtATime()
    {
        var account = NewAccount();

        account.AdvanceSessionRevocationEpochOnce();
        account.AdvanceSessionRevocationEpochOnce();
        account.AdvanceSessionRevocationEpochOnce();

        account.SessionRevocationEpoch.Should().Be(3);
    }

    [Fact]
    public void Epoch_IsStrictlyMonotonic()
    {
        var account = NewAccount();
        var seen = new List<long>();

        for (var i = 0; i < 10; i++)
        {
            account.AdvanceSessionRevocationEpochOnce();
            seen.Add(account.SessionRevocationEpoch);
        }

        seen.Should().Equal(Enumerable.Range(1, 10).Select(i => (long)i));
        seen.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Epoch_RefusesToOverflowAtLongMaxValue()
    {
        var account = NewAccount();
        SetEpoch(account, long.MaxValue);

        var act = () => account.AdvanceSessionRevocationEpochOnce();

        act.Should().Throw<InvalidOperationException>();
        account.SessionRevocationEpoch.Should().Be(long.MaxValue);
    }

    [Fact]
    public void Advance_RaisesExactlyOneDomainEventWithAccountIdAndNewEpoch()
    {
        var account = NewAccount();
        account.ClearDomainEvents();

        account.AdvanceSessionRevocationEpochOnce();

        var raisedEvents = account.DomainEvents.OfType<SessionRevocationEpochAdvancedEvent>().ToList();

        raisedEvents.Should().ContainSingle();
        raisedEvents.Single().UserAccountId.Should().Be(account.Id);
        raisedEvents.Single().Epoch.Should().Be(1);
    }

    [Fact]
    public void Advance_DoesNotRaiseAnyTransportOrOutboxEvent()
    {
        var account = NewAccount();
        account.ClearDomainEvents();

        account.AdvanceSessionRevocationEpochOnce();

        account.DomainEvents.Should().OnlyContain(e => e is SessionRevocationEpochAdvancedEvent);
        account.DomainEvents.Should().HaveCount(1);
        account.DomainEvents.Single().Should().BeAssignableTo<IDomainEvent>();
    }

    private static void SetEpoch(UserAccount account, long value)
    {
        var property = typeof(UserAccount).GetProperty(nameof(UserAccount.SessionRevocationEpoch))!;
        property.SetValue(account, value);
    }
}