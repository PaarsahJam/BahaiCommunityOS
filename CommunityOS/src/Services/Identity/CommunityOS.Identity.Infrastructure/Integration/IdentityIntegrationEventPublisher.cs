using CommunityOS.Contracts.Identity;
using CommunityOS.Identity.Domain.Events;
using CommunityOS.SharedKernel.Domain.Events;
using MassTransit;
using MediatR;

namespace CommunityOS.Identity.Infrastructure.Integration;

/// <summary>
/// Publishes Identity domain events onto the message bus as integration
/// events so other services can react (email verification, notifications,
/// audit, etc.). Runs inside the MediatR pipeline after the originating
/// command has been handled.
/// </summary>
public sealed class IdentityIntegrationEventPublisher(
    IPublishEndpoint publishEndpoint) : INotificationHandler<IDomainEvent>
{
    public async Task Handle(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        switch (domainEvent)
        {
            case UserAccountRegisteredEvent e:
                await publishEndpoint.Publish(
                    new UserAccountRegistered(e.UserAccountId, e.Email, e.OccurredOn), cancellationToken);
                break;
            case UserAccountVerifiedEvent e:
                await publishEndpoint.Publish(
                    new UserAccountVerified(e.UserAccountId, e.OccurredOn), cancellationToken);
                break;
            case UserAccountLockedEvent e:
                await publishEndpoint.Publish(
                    new UserAccountLocked(e.UserAccountId, e.OccurredOn), cancellationToken);
                break;
            case UserAccountUnlockedEvent e:
                await publishEndpoint.Publish(
                    new UserAccountUnlocked(e.UserAccountId, e.OccurredOn), cancellationToken);
                break;
            case UserAccountDeactivatedEvent e:
                await publishEndpoint.Publish(
                    new UserAccountDeactivated(e.UserAccountId, e.OccurredOn), cancellationToken);
                break;
            case CredentialChangedEvent e:
                await publishEndpoint.Publish(
                    new CredentialChanged(e.UserAccountId, e.OccurredOn), cancellationToken);
                break;
            case ExternalIdentityLinkedEvent e:
                await publishEndpoint.Publish(
                    new ExternalIdentityLinked(e.UserAccountId, e.Provider, e.Subject, e.OccurredOn), cancellationToken);
                break;
            case ExternalIdentityUnlinkedEvent e:
                await publishEndpoint.Publish(
                    new ExternalIdentityUnlinked(e.UserAccountId, e.Provider, e.Subject, e.OccurredOn), cancellationToken);
                break;
            case MfaMethodEnrolledEvent e:
                await publishEndpoint.Publish(
                    new MfaMethodEnrolled(e.UserAccountId, e.MethodType, e.OccurredOn), cancellationToken);
                break;
            case MfaMethodRemovedEvent e:
                await publishEndpoint.Publish(
                    new MfaMethodRemoved(e.UserAccountId, e.MethodType, e.OccurredOn), cancellationToken);
                break;
            case DeviceRegisteredEvent e:
                await publishEndpoint.Publish(
                    new DeviceRegistered(e.UserAccountId, e.DeviceId, e.Name, e.OccurredOn), cancellationToken);
                break;
            case RefreshTokenIssuedEvent e:
                await publishEndpoint.Publish(
                    new RefreshTokenIssued(e.UserAccountId, e.SessionId, e.OccurredOn), cancellationToken);
                break;
        }
    }
}
