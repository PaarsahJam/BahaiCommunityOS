namespace CommunityOS.Notifications.Domain.Exceptions;

public sealed class NotificationNotFoundException(Guid id)
    : Exception($"Notification '{id}' was not found.");

public sealed class NotificationTypeNotFoundException(string code)
    : Exception($"Notification type '{code}' was not found.");

public sealed class NotificationPreferenceNotFoundException(Guid memberId, string typeCode)
    : Exception($"Notification preference for member '{memberId}' and type '{typeCode}' was not found.");

public sealed class InvalidNotificationTransitionException(object subjectId, string from, string to)
    : Exception($"Invalid notification transition from '{from}' to '{to}' for '{subjectId}'.");

public sealed class DuplicateNotificationTypeException(string code)
    : Exception($"A notification type with code '{code}' already exists.");

public sealed class NotificationTypeInUseException(string code)
    : Exception($"Notification type '{code}' is in use by one or more notifications and cannot be retired.");

public sealed class RetiredNotificationTypeUpdateException(string code)
    : Exception($"Notification type '{code}' is retired and cannot be updated.");

public sealed class InvalidNotificationTypeReferenceException(string code)
    : Exception($"Notification type '{code}' does not exist or is retired.");

public sealed class InvalidNotificationChannelException(string channel)
    : Exception($"Notification channel '{channel}' is not a ratified channel (Email, Push, InApp, Sms).");

public sealed class InvalidMemberReferenceException(Guid memberId)
    : Exception($"Member '{memberId}' is not a recipient of this notification.");

public sealed class InvalidScopeReferenceException(Guid organizationUnitId)
    : Exception($"Organization unit '{organizationUnitId}' does not exist.");

public sealed class InvalidNotificationException(string message)
    : Exception(message);