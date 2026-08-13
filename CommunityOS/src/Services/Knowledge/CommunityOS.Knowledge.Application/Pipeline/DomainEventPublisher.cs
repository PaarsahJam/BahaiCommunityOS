using CommunityOS.SharedKernel.Domain.Primitives;
using MediatR;

namespace CommunityOS.Knowledge.Application.Pipeline;

/// <summary>
/// Dispatches the domain events accumulated by an aggregate onto the MediatR
/// bus after a command handler completes its write. Handlers (e.g. the
/// integration event publisher) observe the events and forward them.
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