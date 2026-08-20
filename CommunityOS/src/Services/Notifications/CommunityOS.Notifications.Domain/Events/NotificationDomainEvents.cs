using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Notifications.Domain.Events;

/// <summary>
/// Raised when a notification transitions to <c>Dispatched</c> (every recipient
/// terminal) — the only notification event published to the outbox and exported
/// to consumers. <c>SourceType</c>/<c>SourceId</c> are the stable source fact the
/// notification references (null for free-standing <c>general</c> work).
/// </summary>
public sealed record NotificationDispatchedEvent(
    Guid NotificationId,
    string TypeCode,
    string Channel,
    string? SourceType,
    Guid? SourceId,
    int RecipientCount) : DomainEvent;

/// <summary>Raised when a single recipient is delivered (domain-only; never exported).</summary>
public sealed record NotificationDeliveredEvent(Guid NotificationId, Guid MemberId) : DomainEvent;

/// <summary>Raised when a delivered notification is read by the member (domain-only; never exported).</summary>
public sealed record NotificationReadEvent(Guid NotificationId, Guid MemberId) : DomainEvent;