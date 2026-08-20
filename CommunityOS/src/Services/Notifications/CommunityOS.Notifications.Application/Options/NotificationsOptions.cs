namespace CommunityOS.Notifications.Application.Options;

/// <summary>
/// Application-level Notifications configuration (ratified, ADR-025). Bound from
/// the <c>Notifications</c> configuration section by the Infrastructure layer.
/// </summary>
public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    /// <summary>
    /// Reserved: trusted in-process caller id for future reconcile consumers.
    /// Never presented to other services; only used as an audit/actor
    /// identifier.
    /// </summary>
    public string InternalClientId { get; set; } = string.Empty;
}