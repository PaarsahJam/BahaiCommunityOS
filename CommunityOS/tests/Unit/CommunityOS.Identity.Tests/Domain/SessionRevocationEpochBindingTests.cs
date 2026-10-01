using CommunityOS.Identity.Domain.Aggregates;
using FluentAssertions;

namespace CommunityOS.Identity.Tests.Domain;

/// <summary>
/// ADR-036 D3/Q3: refresh sessions are bound to the account
/// <c>SessionRevocationEpoch</c> under which they were issued (login / OAuth
/// exchange / rotation), and <see cref="Session.IsStaleRelativeTo"/> detects a
/// family bound to an epoch older than the account's current one.
/// </summary>
public sealed class SessionRevocationEpochBindingTests
{
    private static readonly Guid UserAccountId = Guid.NewGuid();

    [Fact]
    public void Create_binds_the_session_to_the_observed_epoch()
    {
        Session.Create(UserAccountId, Guid.NewGuid(), "hash", TimeSpan.FromDays(30),
                sessionRevocationEpochAtIssue: 0)
            .SessionRevocationEpochAtIssue.Should().Be(0);

        Session.Create(UserAccountId, Guid.NewGuid(), "hash", TimeSpan.FromDays(30),
                sessionRevocationEpochAtIssue: 7)
            .SessionRevocationEpochAtIssue.Should().Be(7);
    }

    [Fact]
    public void Create_defaults_the_binding_to_epoch_zero()
    {
        Session.Create(UserAccountId, Guid.NewGuid(), "hash", TimeSpan.FromDays(30))
            .SessionRevocationEpochAtIssue.Should().Be(0);
    }

    [Fact]
    public void Create_rejects_a_negative_epoch_binding()
    {
        var act = () => Session.Create(
            UserAccountId, Guid.NewGuid(), "hash", TimeSpan.FromDays(30),
            sessionRevocationEpochAtIssue: -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rotate_binds_the_next_session_to_the_epoch_accepted_under_the_lock()
    {
        var session = Session.Create(
            UserAccountId, Guid.NewGuid(), "hash-1", TimeSpan.FromDays(30),
            sessionRevocationEpochAtIssue: 2);

        var rotated = session.Rotate("hash-2", TimeSpan.FromDays(30), sessionRevocationEpochAtIssue: 5);

        rotated.TokenFamilyId.Should().Be(session.TokenFamilyId);
        rotated.SessionRevocationEpochAtIssue.Should().Be(5,
            "the rotated session binds to the account epoch observed under the lock");
        session.RefreshTokenUsed.Should().BeTrue();
    }

    [Fact]
    public void IsStaleRelativeTo_compares_the_binding_against_the_account_epoch()
    {
        var stale = Session.Create(
            UserAccountId, Guid.NewGuid(), "hash", TimeSpan.FromDays(30),
            sessionRevocationEpochAtIssue: 0);
        stale.IsStaleRelativeTo(accountSessionRevocationEpoch: 1).Should().BeTrue();
        stale.IsStaleRelativeTo(accountSessionRevocationEpoch: 0).Should().BeFalse();
        stale.IsStaleRelativeTo(accountSessionRevocationEpoch: -1).Should().BeFalse(
            "a negative account epoch is not a real account state; equal/older bindings stay acceptable");
    }

    [Fact]
    public void IsStaleRelativeTo_current_binding_is_not_stale()
    {
        var session = Session.Create(
            UserAccountId, Guid.NewGuid(), "hash", TimeSpan.FromDays(30),
            sessionRevocationEpochAtIssue: 3);

        session.IsStaleRelativeTo(accountSessionRevocationEpoch: 3).Should().BeFalse();
        session.IsStaleRelativeTo(accountSessionRevocationEpoch: 4).Should().BeTrue();
        session.IsStaleRelativeTo(accountSessionRevocationEpoch: 2).Should().BeFalse(
            "a binding newer than the account epoch is accepted per the agreed ADR-036 model");
    }
}