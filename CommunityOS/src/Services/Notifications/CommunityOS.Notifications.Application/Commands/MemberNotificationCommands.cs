using CommunityOS.Notifications.Application.DTOs;
using CommunityOS.Notifications.Domain.Exceptions;
using CommunityOS.Notifications.Domain.Repositories;
using MediatR;
using static CommunityOS.Notifications.Application.Commands.NotificationCommandHelpers;
using NotificationsEventsPublisher = CommunityOS.Notifications.Application.Pipeline.DomainEventPublisher;

namespace CommunityOS.Notifications.Application.Commands;

/// <summary>
/// Member-safe mark-as-read (ADR-027): the authenticated actor is the
/// recipient. No <c>memberId</c> is supplied by the client; the actor's
/// subject identity established by the server is the only accepted recipient.
/// Recipient relationship is the sole grant; sensitive notifications are not
/// part of the member contract. Any failure surfaces as the equivalent
/// 404/409 transition (no existence oracle, no recipient enumeration).
/// </summary>
public sealed record MarkMemberNotificationReadCommand(Guid ActorId, Guid NotificationId)
    : IRequest<MemberNotificationSummaryDto>;

internal sealed class MarkMemberNotificationReadCommandHandler(
    INotificationRepository notifications,
    IMediator mediator)
    : IRequestHandler<MarkMemberNotificationReadCommand, MemberNotificationSummaryDto>
{
    public async Task<MemberNotificationSummaryDto> Handle(
        MarkMemberNotificationReadCommand cmd, CancellationToken ct)
    {
        var notification = await LoadForMutationAsync(notifications, cmd.NotificationId, ct);

        if (notification.IsSensitive || !notification.IsRecipient(cmd.ActorId))
            throw new NotificationNotFoundException(cmd.NotificationId);

        notification.MarkRead(cmd.ActorId, DateTime.UtcNow);

        // Read events are domain-only (never exported); dispatched through the
        // mediator for in-process observers before the single transaction
        // commits (same convention as the admin mark-read command).
        await NotificationsEventsPublisher.PublishAsync(notification, mediator, ct);
        await notifications.UpdateAsync(notification, ct);

        return notification.ToMemberSummaryDto(cmd.ActorId);
    }
}