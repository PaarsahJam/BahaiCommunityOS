namespace CommunityOS.Events.Domain.Exceptions;

public sealed class EventNotFoundException(Guid eventId)
    : Exception($"Event '{eventId}' was not found.");

public sealed class EventCapacityExceededException(Guid eventId)
    : Exception($"Event '{eventId}' has reached maximum capacity.");

public sealed class EventAlreadyCancelledException(Guid eventId)
    : Exception($"Event '{eventId}' is already cancelled.");
