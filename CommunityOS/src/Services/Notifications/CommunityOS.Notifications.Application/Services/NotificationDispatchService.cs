using CommunityOS.Notifications.Application.Logging;
using CommunityOS.Notifications.Application.Pipeline;
using CommunityOS.Notifications.Domain.Aggregates;
using CommunityOS.Notifications.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Notifications.Application.Services;

/// <summary>
/// Shared dispatch orchestration used by the explicit dispatch command and the
/// background dispatch worker (ADR-025). Applies the member preferences: an
/// opted-out (member, type, channel) recipient is removed before dispatch so an
/// opt-out suppresses that notification. Then drives the aggregate dispatch
/// (<c>InApp</c> delivers directly; Email/SMS/Push fail closed at the first
/// gate) and publishes the domain events BEFORE the caller saves, so the
/// <c>NotificationDispatched</c> outbox row and the notification change commit
/// atomically (ADR-015).
/// </summary>
public sealed class NotificationDispatchService(
    INotificationPreferenceRepository preferences,
    IMediator mediator,
    ILogger<NotificationDispatchService> logger)
{
    public async Task DispatchAsync(Notification notification, CancellationToken ct = default)
    {
        if (notification.IsDispatched)
            return;

        // Preference filtering: opt-outs suppress delivery for the member on
        // this (type, channel). Only recipients with no opt-out survive.
        var optedOut = await preferences.ListDisabledAsync(
            notification.TypeCode,
            notification.Recipients.Select(r => r.MemberId).ToArray(),
            ct);

        foreach (var optOut in optedOut)
            notification.RemoveRecipient(optOut.MemberId);

        notification.Dispatch(DateTime.UtcNow);

        if (notification.Recipients.Count == 0)
            logger.DispatchedToZeroRecipients(notification.Id);

        // Outbox: publish before SaveChanges so the forwarded integration event
        // and the notification change commit atomically (ADR-015, ratified).
        await DomainEventPublisher.PublishAsync(notification, mediator, ct);

        if (notification.IsDispatched)
            logger.NotificationDispatched(notification.Id, notification.TypeCode, notification.Channel.Name);
    }
}