using CommunityOS.Content.Domain.Entities;
using CommunityOS.Content.Domain.Events;
using CommunityOS.Content.Domain.Exceptions;
using CommunityOS.Content.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Content.Domain.Aggregates;

public sealed class Announcement : AggregateRoot<Guid>
{
    private readonly List<MediaAttachment> _attachments = [];

    public string Title { get; private set; }
    public string Body { get; private set; }
    public Language Language { get; private set; }
    public ContentStatus Status { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid LocalUnitId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public IReadOnlyList<MediaAttachment> Attachments => _attachments.AsReadOnly();

    private Announcement(Guid id, string title, string body, Language language,
        Guid authorId, Guid localUnitId) : base(id)
    {
        Title = title;
        Body = body;
        Language = language;
        AuthorId = authorId;
        LocalUnitId = localUnitId;
        Status = ContentStatus.Draft;
        CreatedAt = DateTime.UtcNow;
    }

    public static Announcement Create(string title, string body, Language language,
        Guid authorId, Guid localUnitId)
    {
        Guard.NotNullOrWhiteSpace(title, nameof(title));
        Guard.NotNullOrWhiteSpace(body, nameof(body));
        Guard.NotNull(language, nameof(language));
        Guard.NotDefault(authorId, nameof(authorId));
        Guard.NotDefault(localUnitId, nameof(localUnitId));
        return new Announcement(Guid.NewGuid(), title.Trim(), body.Trim(),
            language, authorId, localUnitId);
    }

    public void Publish()
    {
        if (Status != ContentStatus.Draft)
            throw new InvalidContentStateException("Only drafts can be published.");
        Status = ContentStatus.Published;
        PublishedAt = DateTime.UtcNow;
        RaiseDomainEvent(new AnnouncementPublishedEvent(Id, Title));
    }

    public void Archive()
    {
        if (Status == ContentStatus.Archived)
            throw new InvalidContentStateException("Already archived.");
        Status = ContentStatus.Archived;
        RaiseDomainEvent(new AnnouncementArchivedEvent(Id));
    }

    public void AddAttachment(MediaAttachment attachment)
    {
        Guard.NotNull(attachment, nameof(attachment));
        _attachments.Add(attachment);
    }
}
