using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Notifications.Domain.Aggregates;

namespace CommunityOS.Notifications.Application.Authorization;

/// <summary>
/// Builds the resource-level authorization contexts for a notification
/// (ADR-025). A notification may belong to multiple organization scopes; access
/// is granted when the caller holds the permission at <b>any</b> of the
/// notification's scopes (primary or additional). Every check passes
/// <c>resourceType = "notification"</c> and the notification id so per-notification
/// grants (relationship tuples) and organization-scoped grants compose exactly
/// as in other services. Fail-closed: any inability to establish the grant is a
/// Deny.
/// </summary>
internal static class NotificationAuthorization
{
    /// <summary>
    /// One context per organization-unit scope (primary plus additional scopes),
    /// or a single global resource context when the notification has no unit
    /// scope.
    /// </summary>
    public static IReadOnlyList<AuthorizationContext> ContextsFor(Notification notification)
    {
        var units = notification.AllOrganizationUnitIds.ToList();
        if (units.Count == 0)
            return [new AuthorizationContext(ResourceType: "notification", ResourceId: notification.Id)];

        return units
            .Select(unit => new AuthorizationContext(unit, "notification", notification.Id))
            .ToList();
    }

    /// <summary>
    /// True when the caller holds <paramref name="permission"/> at any of the
    /// notification's organization scopes (or at the global resource scope when
    /// the notification has no unit scope).
    /// </summary>
    public static async Task<bool> HasForNotificationAsync(
        AuthorizationGuard guard,
        Guid actorId,
        string permission,
        Notification notification,
        CancellationToken ct)
    {
        foreach (var context in ContextsFor(notification))
        {
            if (await guard.HasAsync(actorId, permission, context, ct))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Throws <see cref="AuthorizationForbiddenException"/> when the caller does
    /// not hold <paramref name="permission"/> at any of the notification's
    /// scopes.
    /// </summary>
    public static async Task RequireForNotificationAsync(
        AuthorizationGuard guard,
        Guid actorId,
        string permission,
        Notification notification,
        CancellationToken ct)
    {
        if (!await HasForNotificationAsync(guard, actorId, permission, notification, ct))
            throw new AuthorizationForbiddenException(permission);
    }
}