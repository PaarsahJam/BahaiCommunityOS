using CommunityOS.Contracts.Notifications;
using CommunityOS.Notifications.Domain.Events;
using CommunityOS.SharedKernel.Domain.Events;
using MassTransit;
using MediatR;

namespace CommunityOS.Notifications.Infrastructure.Integration;

/// <summary>
/// Publishes Notifications domain events onto the message bus as integration
/// events (ADR-025). <c>NotificationDispatched</c> is the <b>only</b> exported
/// event — it carries identifiers and a recipient count only, never recipient
/// member ids, bodies, names or delivery failures (the ratified privacy rule).
/// Delivered/read events are domain-only and never forwarded. With the bus
/// outbox enabled, <see cref="IPublishEndpoint"/> publishes are captured into
/// the DbContext and delivered after the business transaction commits
/// (ADR-015, ratified at the Notifications integration gate).
/// </summary>
public sealed class NotificationsIntegrationEventPublisher<TDomainEvent>(IPublishEndpoint publishEndpoint)
    : INotificationHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    public async Task Handle(TDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent is NotificationDispatchedEvent e)
        {
            await publishEndpoint.Publish(
                new NotificationDispatched(
                    e.NotificationId,
                    e.TypeCode,
                    e.Channel,
                    e.SourceType ?? string.Empty,
                    e.SourceId,
                    e.RecipientCount,
                    e.OccurredOn), cancellationToken);
        }
    }
}