using CommunityOS.SharedKernel.Domain.Primitives;
using MediatR;

namespace CommunityOS.Notifications.Application.Pipeline;

/// <summary>
/// Dispatches the domain events accumulated by an aggregate onto the MediatR
/// bus. For Notifications the dispatch happens BEFORE SaveChanges: the
/// integration event publisher forwards each domain event into the transactional
/// outbox (bus outbox) attached to the same DbContext, so the outbox rows and
/// the notification change commit atomically (ADR-015, ratified at the
/// Workflow/Notifications outbox gates).
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