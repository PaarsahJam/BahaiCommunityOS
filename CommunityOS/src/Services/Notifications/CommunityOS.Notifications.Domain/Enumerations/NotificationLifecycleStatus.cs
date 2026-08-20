namespace CommunityOS.Notifications.Domain.Enumerations;

using CommunityOS.SharedKernel.Domain.Primitives;

/// <summary>
/// Lifecycle status of a <c>Notification</c> aggregate (ADR-025, decision 3):
/// <c>Draft → Queued → Dispatched</c>. <c>Dispatched</c> is terminal and
/// immutable: reached only when every recipient is terminal, in the same
/// transaction that publishes <c>NotificationDispatched</c>.
/// </summary>
public sealed class NotificationLifecycleStatus : Enumeration<int>
{
    public static readonly NotificationLifecycleStatus Draft = new(1, "Draft");
    public static readonly NotificationLifecycleStatus Queued = new(2, "Queued");
    public static readonly NotificationLifecycleStatus Dispatched = new(3, "Dispatched");

    private NotificationLifecycleStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<NotificationLifecycleStatus> All =>
        [Draft, Queued, Dispatched];

    public static NotificationLifecycleStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown NotificationLifecycleStatus id: {id}");

    public static NotificationLifecycleStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown NotificationLifecycleStatus name: {name}");
}