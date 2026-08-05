using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Content.Domain.Events;

public sealed record AnnouncementPublishedEvent(Guid AnnouncementId, string Title) : DomainEvent;

public sealed record AnnouncementArchivedEvent(Guid AnnouncementId) : DomainEvent;

public sealed record LibraryItemAddedEvent(Guid LibraryItemId, string Title) : DomainEvent;
