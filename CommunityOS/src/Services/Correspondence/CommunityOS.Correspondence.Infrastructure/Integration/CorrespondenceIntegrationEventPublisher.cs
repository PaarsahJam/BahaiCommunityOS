using CommunityOS.Contracts.Correspondence;
using CommunityOS.Correspondence.Domain.Events;
using MassTransit;
using MediatR;

namespace CommunityOS.Correspondence.Infrastructure.Integration;

/// <summary>
/// Forwards ratified letter domain events onto the message bus as integration
/// events (ADR-028 decision 8). Payloads carry identifiers, codes, counts and
/// timestamps only — never subjects, bodies or display lines. With the bus
/// outbox enabled (Correspondence is born with it), <see cref="IPublishEndpoint"/>
/// publishes are captured into the scoped DbContext and delivered after the
/// business transaction commits (ADR-015).
/// </summary>
public sealed class CorrespondenceIntegrationEventPublisher(IPublishEndpoint publishEndpoint) :
    INotificationHandler<LetterSubmittedDomainEvent>,
    INotificationHandler<LetterDispatchedDomainEvent>,
    INotificationHandler<LetterDeliveryConfirmedDomainEvent>,
    INotificationHandler<LetterDeliveryFailedDomainEvent>,
    INotificationHandler<LetterCancelledDomainEvent>
{
    public Task Handle(LetterSubmittedDomainEvent notification, CancellationToken cancellationToken) =>
        publishEndpoint.Publish(new LetterSubmitted(
            notification.LetterId, notification.LetterYear, notification.LetterSequence,
            notification.OrganizationUnitId, notification.CategoryCode, notification.Sensitivity,
            notification.RecipientCount,
            [.. notification.RecipientPersonIds], [.. notification.RecipientUnitIds],
            notification.SubmittedBy, notification.OccurredOn), cancellationToken);

    public Task Handle(LetterDispatchedDomainEvent notification, CancellationToken cancellationToken) =>
        publishEndpoint.Publish(new LetterDispatched(
            notification.LetterId, notification.LetterYear, notification.LetterSequence,
            notification.OrganizationUnitId, notification.MethodCode,
            notification.DispatchedBy, notification.OccurredOn), cancellationToken);

    public Task Handle(LetterDeliveryConfirmedDomainEvent notification, CancellationToken cancellationToken) =>
        publishEndpoint.Publish(new LetterDeliveryConfirmed(
            notification.LetterId, notification.LetterYear, notification.LetterSequence,
            notification.OrganizationUnitId, notification.MethodCode,
            notification.ConfirmedBy, notification.OccurredOn), cancellationToken);

    public Task Handle(LetterDeliveryFailedDomainEvent notification, CancellationToken cancellationToken) =>
        publishEndpoint.Publish(new LetterDeliveryFailed(
            notification.LetterId, notification.LetterYear, notification.LetterSequence,
            notification.OrganizationUnitId, notification.MethodCode, notification.ReasonCode,
            notification.ConfirmedBy, notification.OccurredOn), cancellationToken);

    public Task Handle(LetterCancelledDomainEvent notification, CancellationToken cancellationToken) =>
        publishEndpoint.Publish(new LetterCancelled(
            notification.LetterId, notification.LetterYear, notification.LetterSequence,
            notification.OrganizationUnitId, notification.CancelledFromStatus, notification.ReasonCode,
            notification.CancelledBy, notification.OccurredOn), cancellationToken);
}
