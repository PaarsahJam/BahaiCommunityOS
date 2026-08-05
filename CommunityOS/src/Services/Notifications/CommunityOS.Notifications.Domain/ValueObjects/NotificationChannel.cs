using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Notifications.Domain.ValueObjects;

public sealed class NotificationChannel : Enumeration<int>
{
    public static readonly NotificationChannel Email  = new(1, "Email");
    public static readonly NotificationChannel Push   = new(2, "Push");
    public static readonly NotificationChannel InApp  = new(3, "InApp");
    public static readonly NotificationChannel Sms    = new(4, "SMS");

    private NotificationChannel(int id, string name) : base(id, name) { }

    public static IEnumerable<NotificationChannel> All => [Email, Push, InApp, Sms];

    public static NotificationChannel FromId(int id) =>
        All.FirstOrDefault(c => c.Id == id)
        ?? throw new ArgumentException($"Unknown NotificationChannel id: {id}");
}
