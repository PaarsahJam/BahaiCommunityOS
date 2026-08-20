using CommunityOS.Notifications.Domain.Exceptions;
using CommunityOS.SharedKernel.Domain.Primitives;
using System.Globalization;

namespace CommunityOS.Notifications.Domain.ValueObjects;

/// <summary>
/// The ratified channel catalog (ADR-025, decision 5):
/// <c>Email(1)</c>, <c>Push(2)</c>, <c>InApp(3)</c>, <c>Sms(4)</c>. <c>InApp</c>
/// is domain-owned and implemented in the first gate; Email, SMS and Push are
/// ratified logical channels whose concrete sending is a future provider
/// integration behind <c>INotificationChannelDispatcher</c>. The channel code is
/// the only channel schema that exists; provider payloads are transport details
/// resolved at dispatch time.
/// </summary>
public sealed class NotificationChannel : Enumeration<int>
{
    public static readonly NotificationChannel Email  = new(1, "Email");
    public static readonly NotificationChannel Push   = new(2, "Push");
    public static readonly NotificationChannel InApp  = new(3, "InApp");
    public static readonly NotificationChannel Sms    = new(4, "Sms");

    private NotificationChannel(int id, string name) : base(id, name) { }

    public static IEnumerable<NotificationChannel> All => [Email, Push, InApp, Sms];

    public static NotificationChannel FromId(int id) =>
        All.FirstOrDefault(c => c.Id == id)
        ?? throw new InvalidNotificationChannelException(id.ToString(CultureInfo.InvariantCulture));

    public static NotificationChannel FromName(string name) =>
        All.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidNotificationChannelException(name);
}