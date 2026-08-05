using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Community.Domain.Events;

public sealed record CommunityCreatedEvent(Guid CommunityId, string Name) : DomainEvent;

public sealed record LocalUnitAddedEvent(Guid CommunityId, Guid LocalUnitId, string Name) : DomainEvent;

public sealed record CommunityHierarchyChangedEvent(Guid CommunityId, Guid? NewParentId) : DomainEvent;
