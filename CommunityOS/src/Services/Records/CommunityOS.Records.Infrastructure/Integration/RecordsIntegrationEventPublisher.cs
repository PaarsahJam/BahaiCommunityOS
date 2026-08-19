using CommunityOS.Contracts.Records;
using CommunityOS.Records.Domain.Events;
using CommunityOS.SharedKernel.Domain.Events;
using MassTransit;
using MediatR;

namespace CommunityOS.Records.Infrastructure.Integration;

/// <summary>
/// Publishes Records domain events onto the message bus as integration events
/// (ADR-023). Only stable ids and minimal lifecycle metadata are exported —
/// never field values, secrets, names or hold reasons. The open generic is
/// closed per concrete domain event by MediatR dispatch on the runtime
/// notification type. With the bus outbox enabled, <see cref="IPublishEndpoint"/>
/// publishes are captured into the DbContext and delivered after the business
/// transaction commits (ADR-015, ratified).
/// </summary>
public sealed class RecordsIntegrationEventPublisher<TDomainEvent>(IPublishEndpoint publishEndpoint)
    : INotificationHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    public async Task Handle(TDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        switch (domainEvent)
        {
            case RecordCreatedEvent e:
                await publishEndpoint.Publish(
                    new RecordCreated(e.RecordId, e.Category, e.Status, e.SubjectType, e.SubjectId,
                        e.OrganizationUnitId, e.CreatedBy, e.OccurredOn), cancellationToken);
                break;
            case RecordSubmittedEvent e:
                await publishEndpoint.Publish(
                    new RecordSubmitted(e.RecordId, e.Status, e.OccurredOn), cancellationToken);
                break;
            case RecordUnderReviewEvent e:
                await publishEndpoint.Publish(
                    new RecordUnderReview(e.RecordId, e.ReviewerId, e.OccurredOn), cancellationToken);
                break;
            case RecordVerifiedEvent e:
                await publishEndpoint.Publish(
                    new RecordVerified(e.RecordId, e.VerifiedBy, e.OccurredOn), cancellationToken);
                break;
            case RecordRejectedEvent e:
                await publishEndpoint.Publish(
                    new RecordRejected(e.RecordId, e.RejectedBy, e.OccurredOn), cancellationToken);
                break;
            case RecordCorrectedEvent e:
                await publishEndpoint.Publish(
                    new RecordCorrected(e.RecordId, e.VersionNumber, e.SupersedesVersionNumber,
                        e.CorrectedBy, e.OccurredOn), cancellationToken);
                break;
            case RecordArchivedEvent e:
                await publishEndpoint.Publish(
                    new RecordArchived(e.RecordId, e.OccurredOn), cancellationToken);
                break;
            case RecordDeactivatedEvent e:
                await publishEndpoint.Publish(
                    new RecordDeactivated(e.RecordId, e.OccurredOn), cancellationToken);
                break;
            case RecordRestoredEvent e:
                await publishEndpoint.Publish(
                    new RecordRestored(e.RecordId, e.Status, e.OccurredOn), cancellationToken);
                break;
            case RecordClassifiedEvent e:
                await publishEndpoint.Publish(
                    new RecordClassified(e.RecordId, e.ClassificationCode, e.IsSensitive, e.OccurredOn),
                    cancellationToken);
                break;
            case RecordHoldPlacedEvent e:
                await publishEndpoint.Publish(
                    new RecordHoldPlaced(e.HoldId, e.RecordId, e.HoldType, e.PlacedBy, e.OccurredOn),
                    cancellationToken);
                break;
            case RecordHoldReleasedEvent e:
                await publishEndpoint.Publish(
                    new RecordHoldReleased(e.HoldId, e.RecordId, e.HoldType, e.ReleasedBy, e.OccurredOn),
                    cancellationToken);
                break;
            case RecordRetentionChangedEvent e:
                await publishEndpoint.Publish(
                    new RecordRetentionChanged(e.RecordId, e.RetentionScheduleCode, e.RetentionPeriod,
                        e.OccurredOn), cancellationToken);
                break;
            case RecordEvidenceAttachedEvent e:
                await publishEndpoint.Publish(
                    new RecordEvidenceAttached(e.RecordId, e.DocumentId, e.VersionNumber,
                        e.ReferenceType, e.OccurredOn), cancellationToken);
                break;
            case RecordEvidenceRemovedEvent e:
                await publishEndpoint.Publish(
                    new RecordEvidenceRemoved(e.RecordId, e.DocumentId, e.VersionNumber, e.OccurredOn),
                    cancellationToken);
                break;
            case RecordRetentionExpiredEvent e:
                await publishEndpoint.Publish(
                    new RecordRetentionExpired(e.RecordId, e.RetentionScheduleCode, e.ExpiredOn,
                        e.OccurredOn), cancellationToken);
                break;
        }
    }
}