using CommunityOS.Notifications.Domain.Aggregates;
using CommunityOS.Notifications.Domain.ValueObjects;

namespace CommunityOS.Notifications.Application.Abstractions;

/// <summary>
/// Outbound integration surface to channel providers (ADR-025, decision 5).
/// The first gate has no provider integration: <c>InApp</c> is domain-owned and
/// delivered directly; Email/SMS/Push fail closed with
/// <c>provider-not-configured</c>. A future provider integration implements
/// this boundary and resolves the member's channel destination through the
/// Community API at dispatch time. Recipients are stable member ids; no contact
/// details, names or PII are ever stored.
/// </summary>
public interface INotificationChannelDispatcher
{
    /// <summary>
    /// Attempts to hand a delivered notification to the channel provider.
    /// Returns true when the channel is configured for the notification (InApp
    /// always is); false when the channel has no configured provider (the
    /// recipient transitions to <c>Failed</c>).
    /// </summary>
    bool IsConfigured(Notification notification);

    /// <summary>Resolved channel destination for a recipient, or null when unavailable.</summary>
    Task<string?> ResolveDestinationAsync(
        Guid memberId, NotificationChannel channel, CancellationToken cancellationToken = default);
}