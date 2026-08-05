namespace CommunityOS.Community.Domain.Exceptions;

public sealed class CommunityNotFoundException(Guid communityId)
    : Exception($"Community '{communityId}' was not found.");

public sealed class LocalUnitNotFoundException(Guid localUnitId)
    : Exception($"Local unit '{localUnitId}' was not found.");

public sealed class DuplicateCommunityNameException(string name)
    : Exception($"A community with name '{name}' already exists in this region.");
