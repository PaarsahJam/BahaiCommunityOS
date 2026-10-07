using CommunityOS.Identity.Application.Commands;
using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.Enumerations;
using CommunityOS.Identity.Domain.Events;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Events;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace CommunityOS.Identity.Tests.Application;

/// <summary>
/// ADR-036 Section 19 Q6: emergency invalidation is account-wide and distinct
/// from ordinary revocation — "ordinary revocation does not acquire emergency
/// semantics merely because the epoch mechanism exists". Password change,
/// password reset, MFA removal and logout-style session revocation must revoke
/// sessions while leaving <see cref="UserAccount.SessionRevocationEpoch"/>
/// untouched and must not raise
/// <see cref="SessionRevocationEpochAdvancedEvent"/>.
/// </summary>
public sealed class OrdinaryRevocationDoesNotAdvanceEpochTests
{
    private static UserAccount Account() =>
        UserAccount.Register(Email.Create("member@example.org"), "password-hash");

    private static IUserAccountRepository UserAccountsReturning(UserAccount account)
    {
        var userAccounts = Substitute.For<IUserAccountRepository>();
        userAccounts.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        return userAccounts;
    }

    private static List<IDomainEvent> PublishedDomainEvents(IMediator mediator) =>
        mediator.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IMediator.Publish))
            .Select(c => c.GetArguments()[0])
            .OfType<IDomainEvent>()
            .ToList();

    [Fact]
    public async Task ChangePassword_RevokesAllSessions_ButDoesNotAdvanceTheEpoch()
    {
        var account = Account();
        var userAccounts = UserAccountsReturning(account);

        var hasher = Substitute.For<IPasswordHasher>();
        hasher.Verify("current-password", "password-hash").Returns(true);
        hasher.Hash("new-password").Returns("new-password-hash");

        var sessions = Substitute.For<ISessionRepository>();
        var securityEvents = Substitute.For<ISecurityEventRepository>();
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangePasswordCommandHandler(
            userAccounts, hasher, sessions, securityEvents, mediator);
        await handler.Handle(
            new ChangePasswordCommand(account.Id, "current-password", "new-password"),
            CancellationToken.None);

        await sessions.Received(1).RevokeAllForUserAsync(
            account.Id, "Password changed.", Arg.Any<CancellationToken>());

        account.SessionRevocationEpoch.Should().Be(0,
            "an ordinary password change is not emergency invalidation");

        var published = PublishedDomainEvents(mediator);
        published.Should().Contain(e => e is CredentialChangedEvent,
            "the ordinary domain event must still be published");
        published.Should().NotContain(e => e is SessionRevocationEpochAdvancedEvent,
            "an ordinary password change must not dispatch emergency semantics");
    }

    [Fact]
    public async Task ResetPassword_RevokesAllSessions_ButDoesNotAdvanceTheEpoch()
    {
        var account = Account();
        var userAccounts = UserAccountsReturning(account);

        var recoveryRequests = Substitute.For<IRecoveryRequestRepository>();
        var request = RecoveryRequest.Create(
            account.Id, "stored-token-hash", "PasswordReset", TimeSpan.FromHours(1));
        recoveryRequests.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(request);

        var hasher = Substitute.For<IPasswordHasher>();
        hasher.Hash("new-password").Returns("new-password-hash");

        var sessions = Substitute.For<ISessionRepository>();
        var mediator = Substitute.For<IMediator>();

        var handler = new ResetPasswordCommandHandler(
            recoveryRequests, userAccounts, hasher, sessions, mediator);
        await handler.Handle(
            new ResetPasswordCommand("raw-recovery-token", "new-password"), CancellationToken.None);

        request.IsConsumed.Should().BeTrue("the recovery token really was consumed");
        await sessions.Received(1).RevokeAllForUserAsync(
            account.Id, "Password reset.", Arg.Any<CancellationToken>());

        account.SessionRevocationEpoch.Should().Be(0,
            "an ordinary password reset is not emergency invalidation");
        PublishedDomainEvents(mediator).Should().NotContain(
            e => e is SessionRevocationEpochAdvancedEvent,
            "an ordinary password reset must not dispatch emergency semantics");
    }

    [Fact]
    public async Task RemoveMfa_RevokesAllSessions_ButDoesNotAdvanceTheEpoch()
    {
        var account = Account();
        var first = account.EnrollMfa(MfaMethodType.AuthenticatorApp, "SECRET_A");
        account.VerifyMfa(first.Id);
        var second = account.EnrollMfa(MfaMethodType.AuthenticatorApp, "SECRET_B");
        account.VerifyMfa(second.Id);

        var userAccounts = UserAccountsReturning(account);
        var sessions = Substitute.For<ISessionRepository>();

        var handler = new RemoveMfaCommandHandler(userAccounts, sessions);
        await handler.Handle(new RemoveMfaCommand(account.Id, second.Id), CancellationToken.None);

        await sessions.Received(1).RevokeAllForUserAsync(
            account.Id, "MFA method removed.", Arg.Any<CancellationToken>());

        account.SessionRevocationEpoch.Should().Be(0,
            "an ordinary MFA removal is not emergency invalidation");
        account.DomainEvents.OfType<SessionRevocationEpochAdvancedEvent>().Should().BeEmpty(
            "an ordinary MFA removal must not raise the emergency epoch event");
    }

    [Fact]
    public async Task LogoutStyleSessionRevocation_NeverTouchesTheAccountRow()
    {
        var account = Account();

        var sessions = Substitute.For<ISessionRepository>();
        var sessionId = Guid.NewGuid();
        var existing = Session.Create(
            account.Id, Guid.NewGuid(), "token-hash", TimeSpan.FromDays(30),
            sessionRevocationEpochAtIssue: 0);
        sessions.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(existing);

        var revokeOne = new RevokeSessionCommandHandler(sessions);
        await revokeOne.Handle(
            new RevokeSessionCommand(account.Id, sessionId), CancellationToken.None);

        var revokeOthers = new RevokeOthersCommandHandler(sessions);
        await revokeOthers.Handle(
            new RevokeOthersCommand(account.Id, Guid.NewGuid()), CancellationToken.None);

        await sessions.Received(1).RevokeAllExceptFamilyForUserAsync(
            account.Id, Arg.Any<Guid>(), "User revoked other sessions.", Arg.Any<CancellationToken>());

        // Structural guarantee: neither ordinary logout path is even capable of
        // reaching the account row, so neither can acquire emergency semantics.
        foreach (var handlerType in new[] { typeof(RevokeSessionCommandHandler), typeof(RevokeOthersCommandHandler) })
        {
            handlerType.GetConstructors().Single().GetParameters()
                .Select(p => p.ParameterType)
                .Should().NotContain(typeof(IUserAccountRepository),
                    $"{handlerType.Name} must not be able to advance the session revocation epoch");
        }

        account.SessionRevocationEpoch.Should().Be(0);
    }
}
