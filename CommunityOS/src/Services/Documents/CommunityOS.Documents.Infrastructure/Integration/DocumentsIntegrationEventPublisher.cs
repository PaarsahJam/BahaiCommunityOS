using CommunityOS.Contracts.Documents;
using CommunityOS.Documents.Domain.Events;
using CommunityOS.SharedKernel.Domain.Events;
using MassTransit;
using MediatR;

namespace CommunityOS.Documents.Infrastructure.Integration;

/// <summary>
/// Publishes Documents domain events onto the message bus as integration
/// events so other services can react (ADR-022). Only stable ids, lifecycle
/// state and content metadata are exported — never binary content, secrets,
/// object keys, filenames or names. Consumers resolve ownership through the
/// owning services. The open generic is closed per concrete domain event by
/// MediatR dispatch on the runtime notification type.
/// </summary>
public sealed class DocumentsIntegrationEventPublisher<TDomainEvent>(IPublishEndpoint publishEndpoint)
    : INotificationHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    public async Task Handle(TDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        switch (domainEvent)
        {
            case DocumentCreatedEvent e:
                await publishEndpoint.Publish(
                    new DocumentCreated(e.DocumentId, e.Title, e.Status, e.OrganizationUnitId,
                        e.OwnerType, e.OwnerId, e.CreatedBy, e.OccurredOn), cancellationToken);
                break;
            case DocumentMetadataUpdatedEvent e:
                await publishEndpoint.Publish(
                    new DocumentMetadataUpdated(e.DocumentId, e.Status, e.OrganizationUnitId, e.OccurredOn), cancellationToken);
                break;
            case DocumentVersionAddedEvent e:
                await publishEndpoint.Publish(
                    new DocumentVersionAdded(e.DocumentId, e.VersionId, e.VersionNumber, e.MimeType,
                        e.SizeBytes, e.ContentHash, e.UploadedBy, e.OccurredOn), cancellationToken);
                break;
            case DocumentClassifiedEvent e:
                await publishEndpoint.Publish(
                    new DocumentClassified(e.DocumentId, e.ClassificationCode, e.IsSensitive, e.OccurredOn), cancellationToken);
                break;
            case DocumentArchivedEvent e:
                await publishEndpoint.Publish(
                    new DocumentArchived(e.DocumentId, e.OccurredOn), cancellationToken);
                break;
            case DocumentDeactivatedEvent e:
                await publishEndpoint.Publish(
                    new DocumentDeactivated(e.DocumentId, e.OccurredOn), cancellationToken);
                break;
            case DocumentRestoredEvent e:
                await publishEndpoint.Publish(
                    new DocumentRestored(e.DocumentId, e.Status, e.OccurredOn), cancellationToken);
                break;
            case DocumentScanCompletedEvent e:
                await publishEndpoint.Publish(
                    new DocumentScanCompleted(e.DocumentId, e.VersionId, e.ScanStatus, e.OccurredOn), cancellationToken);
                break;
        }
    }
}