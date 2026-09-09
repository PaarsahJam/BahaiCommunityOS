using CommunityOS.Identity.Application.Commands;
using CommunityOS.Identity.Application.Validators;
using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Enumerations;
using CommunityOS.Identity.Domain.Events;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace CommunityOS.Identity.Tests.Application;

public sealed class RemoveMfaCommandTests
{
    private static UserAccount AccountWithVerifiedMfa() =>
        AccountWithVerifiedMfa(out _);

    private static UserAccount AccountWithVerifiedMfa(out Guid verifiedMethodId)
    {
        var account = UserAccount.Register(Email.Create("member@example.com"), "password-hash");
        var method = account.EnrollMfa(MfaMethodType.AuthenticatorApp, "BASE32SECRET");
        account.VerifyMfa(method.Id);
        verifiedMethodId = method.Id;
        return account;
    }

    private static (UserAccount account, Guid methodA) AccountWithTwoVerifiedMfa()
    {
        var account = UserAccount.Register(Email.Create("member@example.com"), "password-hash");
        var a = account.EnrollMfa(MfaMethodType.AuthenticatorApp, "SECRET_A");
        account.VerifyMfa(a.Id);
        var b = account.EnrollMfa(MfaMethodType.AuthenticatorApp, "SECRET_B");
        account.VerifyMfa(b.Id);
        return (account, a.Id);
    }

    private static IUserAccountRepository RepoReturning(UserAccount account)
    {
        var userAccounts = Substitute.For<IUserAccountRepository>();
        userAccounts.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        return userAccounts;
    }

    private static ISessionRepository Sessions() => Substitute.For<ISessionRepository>();

    [Fact]
    public async Task Handle_OwnerRemovesVerifiedMfaMethod_Succeeds()
    {
        var (account, methodA) = AccountWithTwoVerifiedMfa();
        var userAccounts = RepoReturning(account);
        var sessions = Sessions();

        var handler = new RemoveMfaCommandHandler(userAccounts, sessions);
        await handler.Handle(new RemoveMfaCommand(account.Id, methodA), CancellationToken.None);

        account.MfaMethods.Should().HaveCount(1);
        await userAccounts.Received(1).UpdateAsync(account, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RemovalIsPersisted_AndEventRemainsRaised()
    {
        var (account, methodA) = AccountWithTwoVerifiedMfa();
        var userAccounts = RepoReturning(account);
        var sessions = Sessions();

        var handler = new RemoveMfaCommandHandler(userAccounts, sessions);
        await handler.Handle(new RemoveMfaCommand(account.Id, methodA), CancellationToken.None);

        account.DomainEvents.Select(e => e.GetType().Name)
            .Should().Contain(nameof(MfaMethodRemovedEvent));
    }

    [Fact]
    public async Task Handle_RemainingVerifiedMethod_PermitsRemoval()
    {
        var (account, methodA) = AccountWithTwoVerifiedMfa();
        var userAccounts = RepoReturning(account);
        var sessions = Sessions();

        var handler = new RemoveMfaCommandHandler(userAccounts, sessions);
        await handler.Handle(new RemoveMfaCommand(account.Id, methodA), CancellationToken.None);

        account.MfaMethods.Should().HaveCount(1);
        account.HasVerifiedMfa.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_FinalVerifiedMethod_RejectedWithPolicyException()
    {
        var account = AccountWithVerifiedMfa(out var methodId);
        var userAccounts = RepoReturning(account);
        var sessions = Sessions();

        var handler = new RemoveMfaCommandHandler(userAccounts, sessions);
        var act = async () => await handler.Handle(
            new RemoveMfaCommand(account.Id, methodId), CancellationToken.None);

        await act.Should().ThrowAsync<MfaLastVerifiedMethodException>();
    }

    [Fact]
    public async Task Handle_FinalMfaRejection_LeavesAccountUnchanged_AndDoesNotPersistOrRevoke()
    {
        var account = AccountWithVerifiedMfa(out var methodId);
        var userAccounts = RepoReturning(account);
        var sessions = Sessions();

        var handler = new RemoveMfaCommandHandler(userAccounts, sessions);
        var act = async () => await handler.Handle(
            new RemoveMfaCommand(account.Id, methodId), CancellationToken.None);

        await act.Should().ThrowAsync<MfaLastVerifiedMethodException>();
        account.MfaMethods.Should().ContainSingle();
        account.HasVerifiedMfa.Should().BeTrue();
        await userAccounts.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        await sessions.DidNotReceiveWithAnyArgs()
            .RevokeAllForUserAsync(default, default!, default);
    }

    [Fact]
    public async Task Handle_UnknownMethod_ThrowsNonExistence()
    {
        var account = AccountWithVerifiedMfa(out var ownMethodId);
        var userAccounts = RepoReturning(account);
        var sessions = Sessions();
        var unknownId = Guid.NewGuid();

        var handler = new RemoveMfaCommandHandler(userAccounts, sessions);
        var act = async () => await handler.Handle(
            new RemoveMfaCommand(account.Id, unknownId), CancellationToken.None);

        await act.Should().ThrowAsync<MfaMethodNotFoundException>();
        account.MfaMethods.Should().ContainSingle(m => m.Id == ownMethodId);
        await userAccounts.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        await sessions.DidNotReceiveWithAnyArgs()
            .RevokeAllForUserAsync(default, default!, default);
    }

    [Fact]
    public async Task Handle_AnotherAccountsMethod_ThrowsNonExistenceAndDoesNotMutateOtherAccount()
    {
        var accountA = AccountWithVerifiedMfa();
        var accountB = AccountWithVerifiedMfa(out var methodBId);
        var userAccounts = RepoReturning(accountA);
        var sessions = Sessions();

        var handler = new RemoveMfaCommandHandler(userAccounts, sessions);
        var act = async () => await handler.Handle(
            new RemoveMfaCommand(accountA.Id, methodBId), CancellationToken.None);

        await act.Should().ThrowAsync<MfaMethodNotFoundException>();
        accountB.MfaMethods.Should().ContainSingle(m => m.Id == methodBId);
        accountB.HasVerifiedMfa.Should().BeTrue();
        await userAccounts.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        await sessions.DidNotReceiveWithAnyArgs()
            .RevokeAllForUserAsync(default, default!, default);
    }

    [Fact]
    public async Task UnknownAndNonOwnedMethods_AreEquivalentFailures()
    {
        var accountA = AccountWithVerifiedMfa();
        var accountB = AccountWithVerifiedMfa(out var methodBId);
        var userAccounts = RepoReturning(accountA);
        var sessions = Sessions();

        var handler = new RemoveMfaCommandHandler(userAccounts, sessions);
        var unknownAct = async () => await handler.Handle(
            new RemoveMfaCommand(accountA.Id, Guid.NewGuid()), CancellationToken.None);
        var nonOwnedAct = async () => await handler.Handle(
            new RemoveMfaCommand(accountA.Id, methodBId), CancellationToken.None);

        var unknown = (await unknownAct.Should().ThrowAsync<MfaMethodNotFoundException>()).Which;
        var nonOwned = (await nonOwnedAct.Should().ThrowAsync<MfaMethodNotFoundException>()).Which;
        nonOwned.GetType().Should().Be(unknown.GetType());
    }

    [Fact]
    public async Task Handle_UnknownAccount_ThrowsUserAccountNotFound()
    {
        var userAccountId = Guid.NewGuid();
        var userAccounts = Substitute.For<IUserAccountRepository>();
        userAccounts.GetByIdAsync(userAccountId, Arg.Any<CancellationToken>())
            .Returns((UserAccount?)null);
        var sessions = Sessions();

        var handler = new RemoveMfaCommandHandler(userAccounts, sessions);
        var act = async () => await handler.Handle(
            new RemoveMfaCommand(userAccountId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UserAccountNotFoundException>();
        await userAccounts.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        await sessions.DidNotReceiveWithAnyArgs()
            .RevokeAllForUserAsync(default, default!, default);
    }

    [Fact]
    public async Task Handle_Success_RevokesAllSessionsForTheAccount()
    {
        var (account, methodA) = AccountWithTwoVerifiedMfa();
        var userAccounts = RepoReturning(account);
        var sessions = Sessions();

        var handler = new RemoveMfaCommandHandler(userAccounts, sessions);
        await handler.Handle(new RemoveMfaCommand(account.Id, methodA), CancellationToken.None);

        await sessions.Received(1).RevokeAllForUserAsync(
            account.Id, "MFA method removed.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Command_CarriesOnlyActorAndMethodIdentifier()
    {
        typeof(RemoveMfaCommand).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo(new[]
            {
                nameof(RemoveMfaCommand.UserAccountId),
                nameof(RemoveMfaCommand.MfaMethodId)
            });
    }

    [Fact]
    public void Validator_RejectsEmptyUserAccountId()
    {
        var validator = new RemoveMfaCommandValidator();

        var result = validator.Validate(new RemoveMfaCommand(Guid.Empty, Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RemoveMfaCommand.UserAccountId));
    }

    [Fact]
    public void Validator_RejectsEmptyMfaMethodId()
    {
        var validator = new RemoveMfaCommandValidator();

        var result = validator.Validate(new RemoveMfaCommand(Guid.NewGuid(), Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RemoveMfaCommand.MfaMethodId));
    }
}
