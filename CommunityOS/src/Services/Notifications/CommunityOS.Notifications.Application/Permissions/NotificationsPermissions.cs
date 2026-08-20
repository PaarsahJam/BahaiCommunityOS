namespace CommunityOS.Notifications.Application.Permissions;

/// <summary>
/// Central registry of well-known Notifications permission names
/// (<c>notifications.entity.action</c>) — the ratified matrix (ADR-025).
/// Enforced by the Notifications application layer through the Authorization
/// guard; no local RBAC and no direct Authorization database access. Metadata
/// reads and sensitive-field reads are separate capabilities; distribution and
/// delivery diagnostics have a dedicated sensitive permission.
/// </summary>
public static class NotificationsPermissions
{
    public const string NotificationRead = "notifications.notification.read";
    public const string NotificationReadSensitive = "notifications.notification.read.sensitive";
    public const string NotificationCreate = "notifications.notification.create";
    public const string NotificationSend = "notifications.notification.send";
    public const string NotificationAdmin = "notifications.notification.admin";
    public const string NotificationTypeManage = "notifications.type.manage";
    public const string NotificationTemplateRead = "notifications.template.read";
    public const string NotificationPreferenceManage = "notifications.preference.manage";
}