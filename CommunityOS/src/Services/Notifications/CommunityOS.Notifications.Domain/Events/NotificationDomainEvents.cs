using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Notifications.Domain.Events;

public sealed record NotificationDispatchedEvent(Guid NotificationId, int RecipientCount) : DomainEvent;

public sealed record NotificationDeliveredEvent(Guid NotificationId, Guid MemberId) : DomainEvent;

public sealed record NotificationReadEvent(Guid NotificationId, Guid MemberId) : DomainEvent;
