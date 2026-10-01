using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;

namespace CommunityOS.Identity.Application.Commands;

/// <summary>
/// <para>ADR-036 D3 (Q3): emergency session invalidation seam. Advances the
/// account's <c>SessionRevocationEpoch</c> by exactly one and revokes every
/// active refresh session family, atomically and under the same account-row
/// lock a refresh transaction takes — so a refresh that commits first is
/// subsequently invalidated, and a refresh that waits on the lock observes the
/// advanced post-lock epoch and rejects.</para>
/// <para>This is deliberately the minimal transactional foundation required by
/// Q3: it is not the full account disable/reactivate workflow, it exposes no
/// API, and it publishes only the existing
/// <see cref="Domain.Events.SessionRevocationEpochAdvancedEvent"/> domain event
/// (forwarded to the bus only if/when Q4 wires an integration mapping).</para>
/// </summary>
public sealed record InvalidateAllSessionsCommand(Guid UserAccountId) : IRequest;

internal sealed class InvalidateAllSessionsCommandHandler(
    IUserAccountRepository userAccounts,
    ISessionRepository sessions,
    IUnitOfWork unitOfWork,
    IMediator mediator) : IRequestHandler<InvalidateAllSessionsCommand>
{
    private const string RevocationReason = "Account session revocation epoch advanced.";

    public async Task Handle(InvalidateAllSessionsCommand cmd, CancellationToken ct)
    {
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // The same account-row FOR UPDATE lock a refresh takes, so the two
            // operations serialize; the epoch advance below is authoritative
            // for any refresh that acquires the lock afterwards.
            var account = await userAccounts.GetByIdForUpdateAsync(cmd.UserAccountId, ct)
                ?? throw new UserAccountNotFoundException(cmd.UserAccountId);

            account.AdvanceSessionRevocationEpochOnce();
            await sessions.RevokeAllForUserAsync(account.Id, RevocationReason, ct);

            // Publish inside the transaction (before the commit's single
            // SaveChanges) so any future Q4 integration-event outbox row is
            // captured and committed atomically with the epoch advance.
            foreach (var domainEvent in account.DomainEvents)
                await mediator.Publish(domainEvent, ct);
            account.ClearDomainEvents();

            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }
}