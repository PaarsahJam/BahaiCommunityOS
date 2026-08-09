using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;

namespace CommunityOS.Identity.Application.Commands;

public sealed record ChangePasswordCommand(
    Guid UserAccountId,
    string CurrentPassword,
    string NewPassword) : IRequest;

internal sealed class ChangePasswordCommandHandler(
    IUserAccountRepository userAccounts,
    IPasswordHasher passwordHasher,
    ISessionRepository sessions,
    ISecurityEventRepository securityEvents,
    IMediator mediator) : IRequestHandler<ChangePasswordCommand>
{
    public async Task Handle(ChangePasswordCommand cmd, CancellationToken ct)
    {
        var account = await userAccounts.GetByIdAsync(cmd.UserAccountId, ct)
            ?? throw new UserAccountNotFoundException(cmd.UserAccountId);

        var storedHash = account.GetPasswordHash();
        if (storedHash is null || !passwordHasher.Verify(cmd.CurrentPassword, storedHash))
            throw new InvalidCredentialsException();

        account.UpdatePassword(passwordHasher.Hash(cmd.NewPassword));
        await userAccounts.UpdateAsync(account, ct);

        await sessions.RevokeAllForUserAsync(account.Id, "Password changed.", ct);
        await securityEvents.AddAsync(SecurityEvent.Create(
            account.Id, "Password.Changed"), ct);

        foreach (var domainEvent in account.DomainEvents)
            await mediator.Publish(domainEvent, ct);
        account.ClearDomainEvents();
    }
}
