using CommunityOS.SharedKernel.Domain.Primitives;
using MediatR;

namespace CommunityOS.Finance.Application.Pipeline;

/// <summary>
/// Dispatches the domain events accumulated by an aggregate onto the MediatR
/// bus. For Finance the dispatch happens BEFORE SaveChanges: the integration
/// event publisher forwards each domain event into the transactional outbox
/// (bus outbox) attached to the same DbContext, so the outbox rows and the
/// ledger change commit atomically (ADR-015, ratified).
/// </summary>
public static class DomainEventPublisher
{
    public static async Task PublishAsync(
        Entity<Guid> entity,
        IMediator mediator,
        CancellationToken ct = default)
    {
        foreach (var domainEvent in entity.DomainEvents)
            await mediator.Publish(domainEvent, ct);

        entity.ClearDomainEvents();
    }
}