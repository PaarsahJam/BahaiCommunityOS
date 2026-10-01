using CommunityOS.Identity.Application.Commands;
using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Events;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace CommunityOS.Identity.Tests.Application;

/// <summary>
/// ADR-036 D3/Q3: the emergency invalidation seam advances the epoch and
/// revokes every active refresh family under the same account-row lock, all in
/// one explicit transaction, so concurrent refreshes serialize on the account
/// row. PostgreSQL FOR UPDATE race semantics are validated by the live
/// integration tests.
/// </summary>
public sealed class InvalidateAllSessionsCommandTests
{
    private static (UserAccount Account, IUserAccountRepository UserAccounts,
        ISessionRepository Sessions, IUnitOfWork UnitOfWork, IMediator Mediator,
        InvalidateAllSessionsCommandHandler Handler) Build()
    {
        var account = UserAccount.Register(Email.Create("grace@example.org"), "password-hash");
        account.Verify();

        var userAccounts = Substitute.For<IUserAccountRepository>();
        userAccounts.GetByIdForUpdateAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(account);

        var sessions = Substitute.For<ISessionRepository>();
        sessions.RevokeAllForUserAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        unitOfWork.RollbackAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var mediator = Substitute.For<IMediator>();
        mediator.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new InvalidateAllSessionsCommandHandler(
            userAccounts, sessions, unitOfWork, mediator);

        return (account, userAccounts, sessions, unitOfWork, mediator, handler);
    }

    [Fact]
    public async Task Invalidate_advances_the_epoch_once_and_revokes_every_active_family()
    {
        var (account, userAccounts, sessions, unitOfWork, mediator, handler) = Build();

        await handler.Handle(new InvalidateAllSessionsCommand(account.Id), CancellationToken.None);

        account.SessionRevocationEpoch.Should().Be(1,
            "the emergency exactly-once advance applied");
        await sessions.Received(1).RevokeAllForUserAsync(
            account.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await mediator.Received(1).Publish(
            Arg.Is<SessionRevocationEpochAdvancedEvent>(
                e => e.UserAccountId == account.Id && e.Epoch == 1),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());

        // The epoch was read (and advanced) only through the row-lock loader.
        await userAccounts.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalidate_sequences_lock_then_epoch_decision_then_single_commit()
    {
        var (account, _, sessions, unitOfWork, _, handler) = Build();

        await handler.Handle(new InvalidateAllSessionsCommand(account.Id), CancellationToken.None);

        Received.InOrder(() =>
        {
            unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>());
            sessions.RevokeAllForUserAsync(account.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
            unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
        });

        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalidate_rolls_back_when_the_account_is_unknown()
    {
        var userAccounts = Substitute.For<IUserAccountRepository>();
        userAccounts.GetByIdForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((UserAccount?)null);

        var sessions = Substitute.For<ISessionRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        unitOfWork.RollbackAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var mediator = Substitute.For<IMediator>();

        var handler = new InvalidateAllSessionsCommandHandler(
            userAccounts, sessions, unitOfWork, mediator);

        var act = async () => await handler.Handle(
            new InvalidateAllSessionsCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowExactlyAsync<CommunityOS.Identity.Domain.Exceptions.UserAccountNotFoundException>();
        await unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}