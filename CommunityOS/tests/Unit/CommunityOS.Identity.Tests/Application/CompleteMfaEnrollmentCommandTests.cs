using CommunityOS.Identity.Application.Commands;
using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Enumerations;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace CommunityOS.Identity.Tests.Application;

public sealed class CompleteMfaEnrollmentCommandTests
{
    private const string ValidCode = "123456";
    private const string InvalidCode = "000000";

    private static UserAccount AccountWithPendingMethod() =>
        AccountWithPendingMethod(out _);

    private static UserAccount AccountWithPendingMethod(out Guid methodId)
    {
        var account = UserAccount.Register(Email.Create("member@example.com"), "password-hash");
        var method = account.EnrollMfa(MfaMethodType.AuthenticatorApp, "BASE32SECRET");
        methodId = method.Id;
        return account;
    }

    private static IUserAccountRepository RepoReturning(UserAccount account)
    {
        var userAccounts = Substitute.For<IUserAccountRepository>();
        userAccounts.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        return userAccounts;
    }

    private static ITotpService TotpVerifying(bool result) =>
        TotpVerifying(result, Arg.Any<string>(), Arg.Any<string>());

    private static ITotpService TotpVerifying(bool result, string secret, string code)
    {
        var totp = Substitute.For<ITotpService>();
        totp.Verify(secret, code, Arg.Any<int>()).Returns(result);
        return totp;
    }

    [Fact]
    public async Task Handle_OwnPendingMethod_WithValidCode_CompletesEnrollmentAndPersists()
    {
        var account = AccountWithPendingMethod(out var methodId);
        var userAccounts = RepoReturning(account);
        var totp = TotpVerifying(true);

        var handler = new CompleteMfaEnrollmentCommandHandler(userAccounts, totp);
        await handler.Handle(
            new CompleteMfaEnrollmentCommand(account.Id, methodId, ValidCode), CancellationToken.None);

        var method = account.MfaMethods.Single();
        method.IsVerified.Should().BeTrue();
        method.IsActive.Should().BeTrue();
        await userAccounts.Received(1).UpdateAsync(account, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OwnPendingMethod_WithInvalidCode_ThrowsAndPersistsNothing()
    {
        var account = AccountWithPendingMethod(out var methodId);
        var userAccounts = RepoReturning(account);
        var totp = TotpVerifying(false);

        var handler = new CompleteMfaEnrollmentCommandHandler(userAccounts, totp);
        var act = async () => await handler.Handle(
            new CompleteMfaEnrollmentCommand(account.Id, methodId, InvalidCode), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidMfaCodeException>();
        var method = account.MfaMethods.Single();
        method.IsVerified.Should().BeFalse();
        method.IsActive.Should().BeFalse();
        await userAccounts.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Handle_AnotherAccountsMethod_ThrowsNonExistenceAndNeverMutatesThatAccount()
    {
        var accountA = AccountWithPendingMethod();
        var accountB = AccountWithPendingMethod(out var methodBId);
        var userAccounts = RepoReturning(accountA);
        var totp = Substitute.For<ITotpService>();

        var handler = new CompleteMfaEnrollmentCommandHandler(userAccounts, totp);
        var act = async () => await handler.Handle(
            new CompleteMfaEnrollmentCommand(accountA.Id, methodBId, ValidCode), CancellationToken.None);

        await act.Should().ThrowAsync<MfaMethodNotFoundException>()
            .WithMessage($"MFA method '{methodBId}' was not found.");

        var methodB = accountB.MfaMethods.Single();
        methodB.IsVerified.Should().BeFalse();
        methodB.IsActive.Should().BeFalse();
        await userAccounts.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Handle_UnknownMethod_ThrowsNonExistenceAndPersistsNothing()
    {
        var account = AccountWithPendingMethod(out var ownMethodId);
        var userAccounts = RepoReturning(account);
        var totp = Substitute.For<ITotpService>();

        var handler = new CompleteMfaEnrollmentCommandHandler(userAccounts, totp);
        var act = async () => await handler.Handle(
            new CompleteMfaEnrollmentCommand(account.Id, Guid.NewGuid(), ValidCode), CancellationToken.None);

        await act.Should().ThrowAsync<MfaMethodNotFoundException>();
        var method = account.MfaMethods.Single(m => m.Id == ownMethodId);
        method.IsVerified.Should().BeFalse();
        method.IsActive.Should().BeFalse();
        await userAccounts.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task UnknownAndNonOwnedMethods_AreEquivalentFailures()
    {
        var accountA = AccountWithPendingMethod();
        var accountB = AccountWithPendingMethod(out var methodBId);
        var userAccounts = RepoReturning(accountA);
        var totp = Substitute.For<ITotpService>();

        var handler = new CompleteMfaEnrollmentCommandHandler(userAccounts, totp);
        var unknownAct = async () => await handler.Handle(
            new CompleteMfaEnrollmentCommand(accountA.Id, Guid.NewGuid(), ValidCode), CancellationToken.None);
        var nonOwnedAct = async () => await handler.Handle(
            new CompleteMfaEnrollmentCommand(accountA.Id, methodBId, ValidCode), CancellationToken.None);

        var unknown = (await unknownAct.Should().ThrowAsync<MfaMethodNotFoundException>()).Which;
        var nonOwned = (await nonOwnedAct.Should().ThrowAsync<MfaMethodNotFoundException>()).Which;
        nonOwned.GetType().Should().Be(unknown.GetType());
    }

    [Fact]
    public async Task Handle_UnknownAuthenticatedAccount_ThrowsUserAccountNotFound()
    {
        var userAccountId = Guid.NewGuid();
        var userAccounts = Substitute.For<IUserAccountRepository>();
        userAccounts.GetByIdAsync(userAccountId, Arg.Any<CancellationToken>()).Returns((UserAccount?)null);
        var totp = Substitute.For<ITotpService>();

        var handler = new CompleteMfaEnrollmentCommandHandler(userAccounts, totp);
        var act = async () => await handler.Handle(
            new CompleteMfaEnrollmentCommand(userAccountId, Guid.NewGuid(), ValidCode), CancellationToken.None);

        await act.Should().ThrowAsync<UserAccountNotFoundException>();
        await userAccounts.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public void Command_CarriesOnlyAuthenticatedAccountAndBodyIdentifiers()
    {
        typeof(CompleteMfaEnrollmentCommand).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo(new[]
            {
                nameof(CompleteMfaEnrollmentCommand.UserAccountId),
                nameof(CompleteMfaEnrollmentCommand.MfaMethodId),
                nameof(CompleteMfaEnrollmentCommand.Code)
            });
    }
}