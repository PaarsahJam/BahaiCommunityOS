namespace CommunityOS.Content.Domain.Exceptions;

public sealed class AnnouncementNotFoundException(Guid id)
    : Exception($"Announcement '{id}' was not found.");

public sealed class LibraryItemNotFoundException(Guid id)
    : Exception($"Library item '{id}' was not found.");

public sealed class InvalidContentStateException(string message)
    : Exception(message);
