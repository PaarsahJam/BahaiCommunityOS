using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Notifications.Domain.ValueObjects;

public sealed class NotificationStatus : Enumeration<int>
{
    public static readonly NotificationStatus Pending   = new(1, "Pending");
    public static readonly NotificationStatus Sent      = new(2, "Sent");
    public static readonly NotificationStatus Delivered = new(3, "Delivered");
    public static readonly NotificationStatus Failed    = new(4, "Failed");
    public static readonly NotificationStatus Read      = new(5, "Read");

    private NotificationStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<NotificationStatus> All =>
        [Pending, Sent, Delivered, Failed, Read];

    public static NotificationStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown NotificationStatus id: {id}");
}
