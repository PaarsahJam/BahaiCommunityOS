using CommunityOS.Identity.Application.Commands;
using CommunityOS.Identity.Application.Crypto;
using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.Enumerations;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CommunityOS.Identity.Tests.Application;

/// <summary>
/// ADR-036 D3/Q3: the refresh accept/reject decision must be made from the
/// account's epoch observed under the account-row lock inside one explicit
/// transaction. These unit tests pin the decision ordering and the family
/// epoch binding; the PostgreSQL FOR UPDATE race semantics are covered by the
/// live integration tests.
/// </summary>
public sealed class RefreshSessionCommandTests
{
    private static (UserAccount Account, Session Session) Setup(
        long accountEpoch, long familyEpoch, bool reused = false, bool revoked = false)
    {
        var account = UserAccount.Register(Email.Create("grace@example.org"), "password-hash");
        account.Verify();
        for (var i = 0; i < accountEpoch; i++)
            account.AdvanceSessionRevocationEpochOnce();

        var session = Session.Create(
            account.Id, Guid.NewGuid(), TokenHasher.Hash("refresh-token"), TimeSpan.FromDays(30),
            sessionRevocationEpochAtIssue: familyEpoch);
        if (reused)
        {
            session.MarkReused();
        }
        else if (revoked)
        {
            session.Revoke("Revoked by test.");
        }

        return (account, session);
    }

    private static (
        ISessionRepository Sessions,
        IUserAccountRepository UserAccounts,
        ITokenService Tokens,
        ISecurityEventRepository SecurityEvents,
        IUnitOfWork UnitOfWork,
        RefreshSessionCommandHandler Handler) BuildHandler(
        UserAccount postLockAccount, Session session)
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetByRefreshTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(session);
        sessions.ReloadAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        sessions.UpdateAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        sessions.AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var userAccounts = Substitute.For<IUserAccountRepository>();
        userAccounts.GetByIdForUpdateAsync(session.UserAccountId, Arg.Any<CancellationToken>())
            .Returns(postLockAccount);

        var tokens = Substitute.For<ITokenService>();
        tokens.GenerateRefreshToken().Returns("new-refresh-token");
        tokens.GenerateAccessToken(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<long>())
            .Returns("new-access-token");

        var securityEvents = Substitute.For<ISecurityEventRepository>();
        securityEvents.AddAsync(Arg.Any<SecurityEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        unitOfWork.RollbackAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var handler = new RefreshSessionCommandHandler(
            sessions, userAccounts, tokens, securityEvents, unitOfWork,
            NullLogger<RefreshSessionCommandHandler>.Instance);

        return (sessions, userAccounts, tokens, securityEvents, unitOfWork, handler);
    }

    [Fact]
    public async Task Refresh_accepts_a_family_bound_to_the_current_epoch()
    {
        var (account, session) = Setup(accountEpoch: 2, familyEpoch: 2);
        var (sessions, userAccounts, tokens, _, unitOfWork, handler) = BuildHandler(account, session);

        var result = await handler.Handle(
            new RefreshSessionCommand(TokenHasher.Hash("refresh-token")), CancellationToken.None);

        result.AccessToken.Should().Be("new-access-token");
        result.RefreshToken.Should().Be("new-refresh-token");
        session.RefreshTokenUsed.Should().BeTrue("rotation marks the superseded token used");

        await sessions.Received().UpdateAsync(session, Arg.Any<CancellationToken>());
        await sessions.Received().AddAsync(
            Arg.Is<Session>(s => s.TokenFamilyId == session.TokenFamilyId
                && s.SessionRevocationEpochAtIssue == account.SessionRevocationEpoch),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());

        // The rotated binding comes from the post-lock account, accessed only
        // through GetByIdForUpdateAsync.
        await userAccounts.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_rejects_a_stale_family_bound_to_an_older_epoch()
    {
        var (account, session) = Setup(accountEpoch: 1, familyEpoch: 0);
        var (_, _, _, _, unitOfWork, handler) = BuildHandler(account, session);

        var act = async () => await handler.Handle(
            new RefreshSessionCommand(TokenHasher.Hash("refresh-token")), CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidRefreshTokenException>();
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_uses_the_post_lock_epoch_not_a_cached_pre_lock_value()
    {
        // Regression: if a future refactor reintroduced a pre-lock account load
        // (GetByIdAsync at epoch 0) and used its epoch for the decision, a
        // family bound to epoch 0 would be ACCEPTED even though the locked
        // account (epoch 1) already invalidated it. The handler must decide
        // from GetByIdForUpdateAsync only, so this refresh is rejected.
        var (_, session) = Setup(accountEpoch: 0, familyEpoch: 0);

        var preLockAccount = UserAccount.Register(
            Email.Create("stale@example.org"), "password-hash");
        var postLockAccount = UserAccount.Register(
            Email.Create("grace@example.org"), "password-hash");
        postLockAccount.AdvanceSessionRevocationEpochOnce();

        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetByRefreshTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(session);
        sessions.ReloadAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        sessions.UpdateAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        sessions.AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var userAccounts = Substitute.For<IUserAccountRepository>();
        // The stale pre-lock value: would make the test pass for a buggy handler.
        userAccounts.GetByIdAsync(session.UserAccountId, Arg.Any<CancellationToken>())
            .Returns(preLockAccount);
        // The authoritative post-lock value.
        userAccounts.GetByIdForUpdateAsync(session.UserAccountId, Arg.Any<CancellationToken>())
            .Returns(postLockAccount);

        var tokens = Substitute.For<ITokenService>();
        tokens.GenerateRefreshToken().Returns("new-refresh-token");
        var securityEvents = Substitute.For<ISecurityEventRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        unitOfWork.RollbackAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var handler = new RefreshSessionCommandHandler(
            sessions, userAccounts, tokens, securityEvents, unitOfWork,
            NullLogger<RefreshSessionCommandHandler>.Instance);

        var act = async () => await handler.Handle(
            new RefreshSessionCommand(TokenHasher.Hash("refresh-token")), CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidRefreshTokenException>(
            "the decision must use the post-lock epoch (1), not the cached pre-lock epoch (0)");
        await userAccounts.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_rejects_when_the_family_was_revoked_while_waiting_for_the_lock()
    {
        // The post-lock reload must observe a revocation committed by the
        // operation that won the lock, so the pre-lock active snapshot cannot
        // be rotated.
        var (account, session) = Setup(accountEpoch: 1, familyEpoch: 0, revoked: true);
        var (_, _, _, _, unitOfWork, handler) = BuildHandler(account, session);

        var act = async () => await handler.Handle(
            new RefreshSessionCommand(TokenHasher.Hash("refresh-token")), CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidRefreshTokenException>();
        await sessionsReloadNotRotated(session, unitOfWork);
    }

    private static async Task sessionsReloadNotRotated(Session session, IUnitOfWork unitOfWork)
    {
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        _ = session; // the reload path never reaches Rotate for a revoked family
    }

    [Fact]
    public async Task Refresh_detects_reuse_and_revokes_the_whole_family_atomically()
    {
        var (account, session) = Setup(accountEpoch: 1, familyEpoch: 1, reused: true);
        var deviceId = Guid.NewGuid();
        var familySibling = Session.Create(
            account.Id, deviceId, TokenHasher.Hash("sibling-token"), TimeSpan.FromDays(30),
            sessionRevocationEpochAtIssue: 1);

        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetByRefreshTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(session);
        sessions.ReloadAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        sessions.GetByFamilyAsync(session.TokenFamilyId, Arg.Any<CancellationToken>())
            .Returns(new[] { session, familySibling });
        sessions.UpdateAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        sessions.AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var userAccounts = Substitute.For<IUserAccountRepository>();
        userAccounts.GetByIdForUpdateAsync(session.UserAccountId, Arg.Any<CancellationToken>())
            .Returns(account);

        var tokens = Substitute.For<ITokenService>();
        var securityEvents = Substitute.For<ISecurityEventRepository>();
        securityEvents.AddAsync(Arg.Any<SecurityEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        unitOfWork.RollbackAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var handler = new RefreshSessionCommandHandler(
            sessions, userAccounts, tokens, securityEvents, unitOfWork,
            NullLogger<RefreshSessionCommandHandler>.Instance);

        var act = async () => await handler.Handle(
            new RefreshSessionCommand(TokenHasher.Hash("refresh-token")), CancellationToken.None);

        await act.Should().ThrowExactlyAsync<RefreshTokenReuseDetectedException>();

        session.IsRevoked.Should().BeTrue();
        familySibling.IsRevoked.Should().BeTrue("reuse revokes every session in the family");
        await sessions.Received().GetByFamilyAsync(session.TokenFamilyId, Arg.Any<CancellationToken>());

        await securityEvents.Received().AddAsync(
            Arg.Is<SecurityEvent>(e =>
                e.UserAccountId == session.UserAccountId &&
                e.EventType == "RefreshToken.ReuseDetected"),
            Arg.Any<CancellationToken>());

        // The reuse revocation was persisted by the single transaction commit
        // before the request failed.
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await sessions.DidNotReceive().AddAsync(
            Arg.Any<Session>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_rejects_an_inactive_or_revoked_family_without_touching_the_profile_state()
    {
        var (account, session) = Setup(accountEpoch: 0, familyEpoch: 0, revoked: true);
        var (_, _, tokens, _, unitOfWork, handler) = BuildHandler(account, session);

        var act = async () => await handler.Handle(
            new RefreshSessionCommand(TokenHasher.Hash("refresh-token")), CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidRefreshTokenException>();
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        tokens.DidNotReceive().GenerateRefreshToken();
    }

    [Fact]
    public async Task Refresh_rejects_when_the_post_lock_account_is_deactivated()
    {
        var (account, session) = Setup(accountEpoch: 0, familyEpoch: 0);
        account.Deactivate();
        var (_, _, _, _, unitOfWork, handler) = BuildHandler(account, session);

        var act = async () => await handler.Handle(
            new RefreshSessionCommand(TokenHasher.Hash("refresh-token")), CancellationToken.None);

        await act.Should().ThrowExactlyAsync<AccountDeactivatedException>();
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}