using CommunityOS.Notifications.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Notifications.Domain.Entities;

public sealed class NotificationRecipient : Entity<Guid>
{
    public Guid MemberId { get; }
    public NotificationChannel Channel { get; }
    public NotificationStatus Status { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public string? FailureReason { get; private set; }

    private NotificationRecipient(Guid id, Guid memberId, NotificationChannel channel) : base(id)
    {
        MemberId = memberId;
        Channel = channel;
        Status = NotificationStatus.Pending;
    }

    public static NotificationRecipient Create(Guid memberId, NotificationChannel channel)
    {
        Guard.NotDefault(memberId, nameof(memberId));
        Guard.NotNull(channel, nameof(channel));
        return new NotificationRecipient(Guid.NewGuid(), memberId, channel);
    }

    public void MarkSent() => Status = NotificationStatus.Sent;

    public void MarkDelivered()
    {
        Status = NotificationStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
    }

    public void MarkRead()
    {
        Status = NotificationStatus.Read;
        ReadAt = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        Status = NotificationStatus.Failed;
        FailureReason = reason;
    }
}
