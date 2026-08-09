using CommunityOS.Identity.Application.Crypto;
using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;
using MediatR;

namespace CommunityOS.Identity.Application.Commands;

public sealed record RequestPasswordResetCommand(string Email) : IRequest<RecoveryTokenDto?>;

internal sealed class RequestPasswordResetCommandHandler(
    IUserAccountRepository userAccounts,
    IRecoveryRequestRepository recoveryRequests) : IRequestHandler<RequestPasswordResetCommand, RecoveryTokenDto?>
{
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(2);

    public async Task<RecoveryTokenDto?> Handle(RequestPasswordResetCommand cmd, CancellationToken ct)
    {
        // Respond uniformly whether or not the account exists to avoid enumeration.
        var email = Email.Create(cmd.Email);
        var account = await userAccounts.GetByEmailAsync(email, ct);
        if (account is null) return null;

        var token = TokenHasher.GenerateToken();
        var request = RecoveryRequest.Create(
            account.Id, TokenHasher.Hash(token), "PasswordReset", ResetTokenLifetime);
        await recoveryRequests.AddAsync(request, ct);

        return new RecoveryTokenDto(token, request.ExpiresOn);
    }
}

public sealed record ResetPasswordCommand(string Token, string NewPassword) : IRequest;

internal sealed class ResetPasswordCommandHandler(
    IRecoveryRequestRepository recoveryRequests,
    IUserAccountRepository userAccounts,
    IPasswordHasher passwordHasher,
    ISessionRepository sessions,
    IMediator mediator) : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(ResetPasswordCommand cmd, CancellationToken ct)
    {
        var tokenHash = TokenHasher.Hash(cmd.Token);
        var request = await recoveryRequests.GetByTokenHashAsync(tokenHash, ct)
            ?? throw new Domain.Exceptions.InvalidRecoveryTokenException();

        if (!request.CanBeConsumed)
            throw new Domain.Exceptions.InvalidRecoveryTokenException();

        var account = await userAccounts.GetByIdAsync(request.UserAccountId, ct)
            ?? throw new Domain.Exceptions.UserAccountNotFoundException(request.UserAccountId);

        request.Consume();
        await recoveryRequests.UpdateAsync(request, ct);

        account.UpdatePassword(passwordHasher.Hash(cmd.NewPassword));
        await userAccounts.UpdateAsync(account, ct);

        await sessions.RevokeAllForUserAsync(account.Id, "Password reset.", ct);

        foreach (var domainEvent in account.DomainEvents)
            await mediator.Publish(domainEvent, ct);
        account.ClearDomainEvents();
    }
}
