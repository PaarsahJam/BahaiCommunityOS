using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;

namespace CommunityOS.Identity.Application.Commands;

public sealed record LinkExternalIdentityCommand(
    Guid UserAccountId,
    string Provider,
    string Subject) : IRequest;

internal sealed class LinkExternalIdentityCommandHandler(
    IUserAccountRepository userAccounts,
    IMediator mediator) : IRequestHandler<LinkExternalIdentityCommand>
{
    public async Task Handle(LinkExternalIdentityCommand cmd, CancellationToken ct)
    {
        var account = await userAccounts.GetByIdAsync(cmd.UserAccountId, ct)
            ?? throw new UserAccountNotFoundException(cmd.UserAccountId);

        account.LinkExternalIdentity(cmd.Provider, cmd.Subject);
        await userAccounts.UpdateAsync(account, ct);

        foreach (var domainEvent in account.DomainEvents)
            await mediator.Publish(domainEvent, ct);
        account.ClearDomainEvents();
    }
}

public sealed record UnlinkExternalIdentityCommand(
    Guid UserAccountId,
    string Provider,
    string Subject) : IRequest;

internal sealed class UnlinkExternalIdentityCommandHandler(
    IUserAccountRepository userAccounts,
    IMediator mediator) : IRequestHandler<UnlinkExternalIdentityCommand>
{
    public async Task Handle(UnlinkExternalIdentityCommand cmd, CancellationToken ct)
    {
        var account = await userAccounts.GetByIdAsync(cmd.UserAccountId, ct)
            ?? throw new UserAccountNotFoundException(cmd.UserAccountId);

        account.UnlinkExternalIdentity(cmd.Provider, cmd.Subject);
        await userAccounts.UpdateAsync(account, ct);

        foreach (var domainEvent in account.DomainEvents)
            await mediator.Publish(domainEvent, ct);
        account.ClearDomainEvents();
    }
}
