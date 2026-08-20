using CommunityOS.Notifications.Application.Pipeline;
using CommunityOS.Notifications.Domain.Aggregates;
using CommunityOS.Notifications.Domain.Repositories;
using CommunityOS.Notifications.Domain.ValueObjects;
using MediatR;

namespace CommunityOS.Notifications.Infrastructure.Integration;

/// <summary>
/// Shared reconciliation helpers for the Notifications consumers (ADR-025).
/// Reconcile consumers are create-if-absent and idempotent: duplicate
/// integration events never produce duplicate notifications and never
/// re-dispatch an already-dispatched notification. Reconcile-created
/// notifications carry the type-catalog templates rendered with the stable,
/// non-PII variable set (ids only); the originator is the reconcile actor, never
/// PII.
/// </summary>
public static class NotificationsReconciliation
{
    /// <summary>
    /// Well-known in-process actor recorded as the creator for reconcile-created
    /// notifications (never a real person id; not PII).
    /// </summary>
    public static readonly Guid SystemActorId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public const string WorkflowTaskSourceType = "workflow-task";

    /// <summary>
    /// Creates a queued notification for a source fact if none exists for
    /// (type, source type, source id, channel). Returns the created notification,
    /// or null when one already existed. The unique filtered index plus
    /// <see cref="INotificationRepository.AddIfAbsentAsync"/> keeps this safe
    /// under concurrent duplicate events.
    /// </summary>
    public static async Task<Notification?> CreateIfAbsentAsync(
        INotificationRepository notifications,
        INotificationTypeRepository types,
        IMediator mediator,
        string typeCode,
        string sourceType,
        Guid sourceId,
        IReadOnlyList<Guid> recipientIds,
        IReadOnlyDictionary<string, string> variables,
        DateTime occurredOn,
        CancellationToken ct)
    {
        if (recipientIds.Count == 0)
            return null;

        if (await notifications.GetBySourceAsync(typeCode, sourceType, sourceId, ct) is not null)
            return null;

        var type = await types.GetByCodeAsync(typeCode, ct);
        var template = type is null
            ? MessageTemplate.Create("Notice", "A notification requires your attention.")
            : RenderTemplate(type.SubjectTemplate, type.BodyTemplate, variables);

        var notification = Notification.Create(
            typeCode,
            NotificationChannel.InApp,
            sourceType,
            sourceId,
            template,
            organizationUnitId: null,
            additionalScopes: [],
            scheduledFor: null,
            isSensitive: type?.IsSensitive ?? false,
            recipientIds,
            SystemActorId,
            occurredOn);

        notification.Queue();

        // Reconcile-created notifications are published through the same
        // transactional outbox gate as direct creation (ADR-015): the created
        // domain event is forwarded BEFORE the insert so the NotificationDispatched
        // outbox row and the notification row commit atomically. If the filtered
        // unique index wins a concurrent race, AddIfAbsentAsync rolls back the
        // whole SaveChanges — the uncommitted outbox row is discarded with the
        // scope, so no spurious event is ever emitted for a notification that
        // was not persisted.
        await DomainEventPublisher.PublishAsync(notification, mediator, ct);
        var persisted = await notifications.AddIfAbsentAsync(notification, ct);
        return persisted is not null && persisted.Id == notification.Id ? notification : null;
    }

    /// <summary>
    /// Renders the type-catalog body template with the validated, non-PII
    /// variable set (ids, dates, codes). Notifications never embeds record field
    /// values, task notes, hold reasons or person names in a body (ADR-021
    /// citation rule; ADR-025).
    /// </summary>
    private static MessageTemplate RenderTemplate(
        string subject, string body, IReadOnlyDictionary<string, string> variables) =>
        MessageTemplate.Create(subject, MessageTemplate.Create(subject, body).Render(variables));
}