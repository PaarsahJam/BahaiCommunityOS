namespace CommunityOS.Notifications.Domain.Enumerations;

using CommunityOS.SharedKernel.Domain.Primitives;

/// <summary>
/// Per-recipient delivery state (ADR-025, decision 4):
/// <c>Pending → Sent → Delivered → Read</c>, with <c>Failed</c> terminal from
/// <c>Pending</c>/<c>Sent</c>. <c>Sent</c> = handed to a channel provider;
/// <c>InApp</c> skips <c>Sent</c> and delivers directly. <c>Read</c> is terminal
/// and only legal from <c>Delivered</c>; <c>Failed</c> is terminal and records a
/// <c>FailureReason</c>. No regress and no reopen — a re-send is a new
/// notification.
/// </summary>
public sealed class NotificationRecipientStatus : Enumeration<int>
{
    public static readonly NotificationRecipientStatus Pending = new(1, "Pending");
    public static readonly NotificationRecipientStatus Sent = new(2, "Sent");
    public static readonly NotificationRecipientStatus Delivered = new(3, "Delivered");
    public static readonly NotificationRecipientStatus Failed = new(4, "Failed");
    public static readonly NotificationRecipientStatus Read = new(5, "Read");

    private NotificationRecipientStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<NotificationRecipientStatus> All =>
        [Pending, Sent, Delivered, Failed, Read];

    public static NotificationRecipientStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown NotificationRecipientStatus id: {id}");

    public static NotificationRecipientStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown NotificationRecipientStatus name: {name}");
}