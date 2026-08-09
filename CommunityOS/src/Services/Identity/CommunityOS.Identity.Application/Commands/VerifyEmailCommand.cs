using CommunityOS.Identity.Application.Crypto;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;

namespace CommunityOS.Identity.Application.Commands;

public sealed record VerifyEmailCommand(string Token) : IRequest;

internal sealed class VerifyEmailCommandHandler(
    IRecoveryRequestRepository recoveryRequests,
    IUserAccountRepository userAccounts,
    IMediator mediator) : IRequestHandler<VerifyEmailCommand>
{
    public async Task Handle(VerifyEmailCommand cmd, CancellationToken ct)
    {
        var tokenHash = TokenHasher.Hash(cmd.Token);
        var request = await recoveryRequests.GetByTokenHashAsync(tokenHash, ct)
            ?? throw new InvalidRecoveryTokenException();

        if (!request.CanBeConsumed)
            throw new InvalidRecoveryTokenException();

        var account = await userAccounts.GetByIdAsync(request.UserAccountId, ct)
            ?? throw new UserAccountNotFoundException(request.UserAccountId);

        request.Consume();
        await recoveryRequests.UpdateAsync(request, ct);

        account.Verify();
        await userAccounts.UpdateAsync(account, ct);

        foreach (var domainEvent in account.DomainEvents)
            await mediator.Publish(domainEvent, ct);
        account.ClearDomainEvents();
    }
}
