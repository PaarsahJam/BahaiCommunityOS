using MediatR;

namespace CommunityOS.SharedKernel.Domain.Events;

/// <summary>
/// A domain event raised by an aggregate. Implements MediatR's INotification
/// so handlers can be published in-process via the mediator pipeline.
/// </summary>
public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
}
