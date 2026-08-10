using CommunityOS.SharedKernel.Domain.Primitives;
using MediatR;

namespace CommunityOS.Authorization.Application.Authorization;

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
