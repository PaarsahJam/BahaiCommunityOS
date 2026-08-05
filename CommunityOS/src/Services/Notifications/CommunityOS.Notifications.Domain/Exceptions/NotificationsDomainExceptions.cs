namespace CommunityOS.Notifications.Domain.Exceptions;

public sealed class NotificationNotFoundException(Guid id)
    : Exception($"Notification '{id}' was not found.");
