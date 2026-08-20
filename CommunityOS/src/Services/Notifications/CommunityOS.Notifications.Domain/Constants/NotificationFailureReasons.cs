namespace CommunityOS.Notifications.Domain.Constants;

/// <summary>Stable failure-reason codes persisted on <c>NotificationRecipient.FailureReason</c>.</summary>
public static class NotificationFailureReasons
{
    /// <summary>Channel has no configured provider (Email/SMS/Push at the first gate).</summary>
    public const string ProviderNotConfigured = "provider-not-configured";
}