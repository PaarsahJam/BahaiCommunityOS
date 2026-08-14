using CommunityOS.Authorization.Domain.Events;
using CommunityOS.Contracts.Authorization;
using CommunityOS.SharedKernel.Domain.Events;
using MassTransit;
using MediatR;

namespace CommunityOS.Authorization.Infrastructure.Integration;

/// <summary>
/// Publishes Authorization domain events onto the message bus as integration
/// events so other services can react (audit, caching of authorization state,
/// notifications). Runs inside the MediatR pipeline after the originating
/// command has been handled. No sensitive authorization internals or PII are
/// placed on the bus.
/// </summary>
public sealed class AuthorizationIntegrationEventPublisher<TDomainEvent>(IPublishEndpoint publishEndpoint)
    : INotificationHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    public async Task Handle(TDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        switch (domainEvent)
        {
            case RoleAssignedEvent e:
                await publishEndpoint.Publish(
                    new RoleAssigned(
                        e.AssignmentId,
                        e.SubjectId,
                        e.RoleCode,
                        e.ScopeType,
                        e.ScopeId,
                        e.EffectiveFrom,
                        e.EffectiveUntil,
                        e.OccurredOn),
                    cancellationToken);
                break;
            case RoleRevokedEvent e:
                await publishEndpoint.Publish(
                    new RoleRevoked(e.AssignmentId, e.SubjectId, e.RoleCode, e.OccurredOn),
                    cancellationToken);
                break;
            case DelegationGrantedEvent e:
                await publishEndpoint.Publish(
                    new DelegationGranted(e.DelegationId, e.DelegatorId, e.DelegateId, e.OccurredOn),
                    cancellationToken);
                break;
            case DelegationRevokedEvent e:
                await publishEndpoint.Publish(
                    new DelegationRevoked(e.DelegationId, e.DelegatorId, e.DelegateId, e.OccurredOn),
                    cancellationToken);
                break;
            case BreakGlassRequestedEvent e:
                await publishEndpoint.Publish(
                    new BreakGlassRequested(e.RequestId, e.RequesterId, e.OccurredOn),
                    cancellationToken);
                break;
            case BreakGlassApprovedEvent e:
                await publishEndpoint.Publish(
                    new BreakGlassApproved(e.RequestId, e.RequesterId, e.ApproverId, e.ApprovedUntil, e.OccurredOn),
                    cancellationToken);
                break;
            case BreakGlassRevokedEvent e:
                await publishEndpoint.Publish(
                    new BreakGlassRevoked(e.RequestId, e.RequesterId, e.RevokedBy, e.OccurredOn),
                    cancellationToken);
                break;
        }
    }
}
