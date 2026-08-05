using CommunityOS.Notifications.Domain.Entities;
using CommunityOS.Notifications.Domain.Events;
using CommunityOS.Notifications.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Notifications.Domain.Aggregates;

public sealed class Notification : AggregateRoot<Guid>
{
    private readonly List<NotificationRecipient> _recipients = [];

    public MessageTemplate Template { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ScheduledFor { get; private set; }

    public IReadOnlyList<NotificationRecipient> Recipients => _recipients.AsReadOnly();

    private Notification(Guid id, MessageTemplate template,
        NotificationChannel channel, DateTime? scheduledFor) : base(id)
    {
        Template = template;
        Channel = channel;
        CreatedAt = DateTime.UtcNow;
        ScheduledFor = scheduledFor;
    }

    public static Notification Create(MessageTemplate template,
        NotificationChannel channel, DateTime? scheduledFor = null)
    {
        Guard.NotNull(template, nameof(template));
        Guard.NotNull(channel, nameof(channel));
        return new Notification(Guid.NewGuid(), template, channel, scheduledFor);
    }

    public void AddRecipient(Guid memberId)
    {
        Guard.NotDefault(memberId, nameof(memberId));
        if (_recipients.Any(r => r.MemberId == memberId)) return;
        _recipients.Add(NotificationRecipient.Create(memberId, Channel));
    }

    public void MarkDispatched()
    {
        foreach (var recipient in _recipients)
            recipient.MarkSent();
        RaiseDomainEvent(new NotificationDispatchedEvent(Id, _recipients.Count));
    }
}
