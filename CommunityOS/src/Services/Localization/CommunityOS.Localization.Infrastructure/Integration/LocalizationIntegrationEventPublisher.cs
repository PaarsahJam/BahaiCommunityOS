using CommunityOS.Contracts.Localization;
using CommunityOS.Localization.Domain.Events;
using MassTransit;
using MediatR;

namespace CommunityOS.Localization.Infrastructure.Integration;

/// <summary>
/// Forwards the ratified catalog domain event onto the message bus as the
/// single <see cref="LocalizationCatalogChanged"/> integration fact (ADR-029
/// decisions 14/16). Payloads carry codes, a version and a timestamp only —
/// never translation values or any catalog content. With the bus outbox
/// enabled from birth, <see cref="IPublishEndpoint"/> publishes are captured
/// into the scoped DbContext and delivered after the business transaction
/// commits (ADR-015). Localization has ZERO consumers at this gate.
/// </summary>
public sealed class LocalizationIntegrationEventPublisher(IPublishEndpoint publishEndpoint) :
    INotificationHandler<CatalogChangedDomainEvent>
{
    public Task Handle(CatalogChangedDomainEvent notification, CancellationToken cancellationToken) =>
        publishEndpoint.Publish(new LocalizationCatalogChanged(
            notification.CatalogContext,
            notification.Namespace,
            notification.Culture,
            notification.BundleVersion,
            notification.OccurredOn), cancellationToken);
}
