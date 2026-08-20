namespace CommunityOS.Notifications.Domain.Entities;

/// <summary>
/// A single organization-unit scope attached to a notification (primary scope is
/// a scalar on <c>Notification</c>; this is an additional scope). Scope access is
/// any-of: the caller is granted access when holding the permission at any of the
/// notification's scopes (Records/Workflow pattern, ADR-025 decision 8).
/// </summary>
public sealed class NotificationScope
{
    private NotificationScope() : this(Guid.Empty, Guid.Empty)
    {
    }

    public NotificationScope(Guid id, Guid organizationUnitId)
    {
        Id = id;
        OrganizationUnitId = organizationUnitId;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationUnitId { get; private set; }
}