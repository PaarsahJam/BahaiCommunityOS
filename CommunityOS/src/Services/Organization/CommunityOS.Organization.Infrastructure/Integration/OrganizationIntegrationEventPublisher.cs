using CommunityOS.Contracts.Organization;
using CommunityOS.Organization.Domain.Events;
using CommunityOS.SharedKernel.Domain.Events;
using MassTransit;
using MediatR;

namespace CommunityOS.Organization.Infrastructure.Integration;

/// <summary>
/// Publishes Organization domain events onto the message bus as integration
/// events so other services (Community profile linking, Authorization
/// organization-context provisioning, audit) can react. Runs inside the
/// MediatR pipeline after the originating command has been handled. Person
/// identity is carried only as a stable id — never PII.
/// </summary>
public sealed class OrganizationIntegrationEventPublisher<TDomainEvent>(IPublishEndpoint publishEndpoint)
    : INotificationHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    public async Task Handle(TDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        switch (domainEvent)
        {
            case OrganizationCreatedEvent e:
                await publishEndpoint.Publish(
                    new OrganizationCreated(
                        e.OrganizationId, e.Name, e.OrganizationType, e.JurisdictionType, e.JurisdictionScopeId, e.OccurredOn),
                    cancellationToken);
                break;
            case OrganizationUpdatedEvent e:
                await publishEndpoint.Publish(
                    new OrganizationUpdated(
                        e.OrganizationId, e.Name, e.OrganizationType, e.JurisdictionType, e.JurisdictionScopeId, e.OccurredOn),
                    cancellationToken);
                break;
            case OrganizationUnitCreatedEvent e:
                await publishEndpoint.Publish(
                    new OrganizationUnitCreated(
                        e.OrganizationUnitId, e.OrganizationId, e.Name, e.UnitType, e.ParentId, e.OccurredOn),
                    cancellationToken);
                break;
            case OrganizationUnitUpdatedEvent e:
                await publishEndpoint.Publish(
                    new OrganizationUnitUpdated(e.OrganizationUnitId, e.Name, e.UnitType, e.OccurredOn),
                    cancellationToken);
                break;
            case OrganizationUnitParentChangedEvent e:
                await publishEndpoint.Publish(
                    new OrganizationUnitParentChanged(
                        e.OrganizationUnitId, e.ParentId, e.EffectiveFrom, e.EffectiveUntil, e.OccurredOn),
                    cancellationToken);
                break;
            case AppointmentAssignedEvent e:
                await publishEndpoint.Publish(
                    new AppointmentAssigned(
                        e.AppointmentId, e.PersonId, e.OrganizationUnitId, e.AppointmentType,
                        e.EffectiveFrom, e.EffectiveUntil, e.OccurredOn),
                    cancellationToken);
                break;
            case AppointmentEndedEvent e:
                await publishEndpoint.Publish(
                    new AppointmentEnded(e.AppointmentId, e.PersonId, e.OrganizationUnitId, e.OccurredOn),
                    cancellationToken);
                break;
            case CommitteeCreatedEvent e:
                await publishEndpoint.Publish(
                    new CommitteeCreated(
                        e.CommitteeId, e.Name, e.CommitteeType, e.OrganizationId, e.OrganizationUnitId,
                        e.JurisdictionType, e.JurisdictionScopeId, e.OccurredOn),
                    cancellationToken);
                break;
            case CommitteeMemberAddedEvent e:
                await publishEndpoint.Publish(
                    new CommitteeMemberAdded(
                        e.CommitteeId, e.PersonId, e.RoleCode, e.EffectiveFrom, e.EffectiveUntil, e.OccurredOn),
                    cancellationToken);
                break;
            case CommitteeMemberRemovedEvent e:
                await publishEndpoint.Publish(
                    new CommitteeMemberRemoved(e.CommitteeId, e.PersonId, e.RoleCode, e.OccurredOn),
                    cancellationToken);
                break;
            case DelegationFactGrantedEvent e:
                await publishEndpoint.Publish(
                    new DelegationFactGranted(
                        e.DelegationFactId, e.DelegatorId, e.DelegateId, e.OrganizationUnitId,
                        e.DelegationType, e.EffectiveFrom, e.EffectiveUntil, e.OccurredOn),
                    cancellationToken);
                break;
            case DelegationFactRevokedEvent e:
                await publishEndpoint.Publish(
                    new DelegationFactRevoked(
                        e.DelegationFactId, e.DelegatorId, e.DelegateId, e.OrganizationUnitId, e.OccurredOn),
                    cancellationToken);
                break;
        }
    }
}