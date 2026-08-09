using CommunityOS.Identity.Application.Crypto;
using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;
using MediatR;

namespace CommunityOS.Identity.Application.Commands;

public sealed record RegisterCommand(
    string Email,
    string Password) : IRequest<VerificationTokenDto>;

internal sealed class RegisterCommandHandler(
    IUserAccountRepository userAccounts,
    IRecoveryRequestRepository recoveryRequests,
    IPasswordHasher passwordHasher,
    IMediator mediator) : IRequestHandler<RegisterCommand, VerificationTokenDto>
{
    private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(24);

    public async Task<VerificationTokenDto> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        var email = Email.Create(cmd.Email);

        if (await userAccounts.ExistsByEmailAsync(email, ct))
            throw new DuplicateEmailException(email.Value);

        var account = UserAccount.Register(email, passwordHasher.Hash(cmd.Password));
        await userAccounts.AddAsync(account, ct);

        var token = TokenHasher.GenerateToken();
        var request = RecoveryRequest.Create(
            account.Id, TokenHasher.Hash(token), "EmailVerification", VerificationTokenLifetime);
        await recoveryRequests.AddAsync(request, ct);

        foreach (var domainEvent in account.DomainEvents)
            await mediator.Publish(domainEvent, ct);
        account.ClearDomainEvents();

        return new VerificationTokenDto(token, request.ExpiresOn);
    }
}
