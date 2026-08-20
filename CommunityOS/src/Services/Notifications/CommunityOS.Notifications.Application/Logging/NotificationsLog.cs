using Microsoft.Extensions.Logging;

namespace CommunityOS.Notifications.Application.Logging;

/// <summary>
/// Structured, parameterized logging for the Notifications service. Log messages
/// never include subject/body copy, recipient distributions, names or secrets
/// (ADR-025).
/// </summary>
public static partial class NotificationsLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Notification {NotificationId} created (type {TypeCode}).")]
    public static partial void NotificationCreated(this ILogger logger, Guid notificationId, string typeCode);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Notification {NotificationId} dispatched (type {TypeCode}, channel {Channel}).")]
    public static partial void NotificationDispatched(this ILogger logger, Guid notificationId, string typeCode, string channel);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Administrative override applied on notification {NotificationId}: {Action}.")]
    public static partial void AdminOverrideApplied(this ILogger logger, Guid notificationId, string action);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Unhandled exception: {Message}")]
    public static partial void UnhandledException(this ILogger logger, Exception exception, string message);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Notification type {Code} created.")]
    public static partial void NotificationTypeCreated(this ILogger logger, string code);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "Notification type {Code} retired.")]
    public static partial void NotificationTypeRetired(this ILogger logger, string code);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Warning,
        Message = "Notification {NotificationId} dispatched to zero recipients after preference filtering.")]
    public static partial void DispatchedToZeroRecipients(this ILogger logger, Guid notificationId);
}