using CommunityOS.Community.Domain.Events;
using CommunityOS.Contracts.Community;
using CommunityOS.SharedKernel.Domain.Events;
using MassTransit;
using MediatR;

namespace CommunityOS.Community.Infrastructure.Integration;

/// <summary>
/// Publishes Community domain events onto the message bus as integration
/// events so other services can react. Only stable ids and lifecycle status
/// are placed on the bus — PII (names, contact details, dates of birth) is
/// never exported; consumers read profile data through the Community API.
/// </summary>
public sealed class CommunityIntegrationEventPublisher<TDomainEvent>(IPublishEndpoint publishEndpoint)
    : INotificationHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    public async Task Handle(TDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        switch (domainEvent)
        {
            case PersonCreatedEvent e:
                await publishEndpoint.Publish(
                    new PersonCreated(e.PersonId, e.Status, e.OccurredAt), cancellationToken);
                break;
            case PersonProfileUpdatedEvent e:
                await publishEndpoint.Publish(
                    new PersonUpdated(e.PersonId, e.Status, e.OccurredAt), cancellationToken);
                break;
            case PersonDeactivatedEvent e:
                await publishEndpoint.Publish(
                    new PersonDeactivated(e.PersonId, e.OccurredAt), cancellationToken);
                break;
            case PersonReactivatedEvent e:
                await publishEndpoint.Publish(
                    new PersonUpdated(e.PersonId, "active", e.OccurredAt), cancellationToken);
                break;
            case PersonIdentityLinkedEvent e:
                await publishEndpoint.Publish(
                    new PersonIdentityLinked(e.PersonId, e.IdentityAccountId, e.OccurredAt), cancellationToken);
                break;
            case PersonIdentityUnlinkedEvent e:
                await publishEndpoint.Publish(
                    new PersonIdentityUnlinked(e.PersonId, e.IdentityAccountId, e.OccurredAt), cancellationToken);
                break;
            case HouseholdCreatedEvent e:
                await publishEndpoint.Publish(
                    new HouseholdCreated(e.HouseholdId, e.OccurredAt), cancellationToken);
                break;
            case FamilyRelationshipCreatedEvent:
            case FamilyRelationshipEndedEvent:
                break;
            case MembershipChangedEvent e:
                await publishEndpoint.Publish(
                    new MembershipChanged(
                        e.MembershipId,
                        e.PersonId,
                        e.Status,
                        e.EffectiveFrom,
                        e.EffectiveUntil,
                        e.OccurredAt), cancellationToken);
                break;
            case ActivityCreatedEvent e:
                await publishEndpoint.Publish(
                    new ActivityCreated(e.ActivityId, e.OrganizationUnitId, e.Status, e.OccurredAt), cancellationToken);
                break;
            case ActivityUpdatedEvent e:
                await publishEndpoint.Publish(
                    new ActivityUpdated(e.ActivityId, e.OrganizationUnitId, e.Status, e.OccurredAt), cancellationToken);
                break;
            case CommunityEventCreatedEvent e:
                await publishEndpoint.Publish(
                    new CommunityEventCreated(e.CommunityEventId, e.Title, e.StartsAt, e.OrganizationUnitId, e.Status, e.OccurredAt),
                    cancellationToken);
                break;
            case CommunityEventUpdatedEvent e:
                await publishEndpoint.Publish(
                    new CommunityEventUpdated(e.CommunityEventId, e.StartsAt, e.OrganizationUnitId, e.Status, e.OccurredAt),
                    cancellationToken);
                break;
            case MeetingCreatedEvent e:
                await publishEndpoint.Publish(
                    new MeetingCreated(e.MeetingId, e.Title, e.StartsAt, e.OrganizationUnitId, e.Status, e.OccurredAt),
                    cancellationToken);
                break;
            case MeetingUpdatedEvent e:
                break;
            case MeetingRecordedEvent e:
                await publishEndpoint.Publish(
                    new MeetingRecorded(e.MeetingId, e.OrganizationUnitId, e.OccurredAt), cancellationToken);
                break;
            case ParticipationRecordedEvent e:
                await publishEndpoint.Publish(
                    new ParticipationRecorded(
                        e.ParticipationId,
                        e.PersonId,
                        e.TargetType,
                        e.TargetId,
                        e.Status,
                        e.OccurredAt), cancellationToken);
                break;
        }
    }
}
