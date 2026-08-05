using CommunityOS.Content.Domain.Entities;
using CommunityOS.Content.Domain.Events;
using CommunityOS.Content.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Content.Domain.Aggregates;

public sealed class LibraryItem : AggregateRoot<Guid>
{
    private readonly List<MediaAttachment> _attachments = [];

    public string Title { get; private set; }
    public string? Description { get; private set; }
    public Language Language { get; private set; }
    public ContentStatus Status { get; private set; }
    public string Category { get; private set; }
    public Guid UploadedById { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<MediaAttachment> Attachments => _attachments.AsReadOnly();

    private LibraryItem(Guid id, string title, string? description, Language language,
        string category, Guid uploadedById) : base(id)
    {
        Title = title;
        Description = description;
        Language = language;
        Category = category;
        UploadedById = uploadedById;
        Status = ContentStatus.Draft;
        CreatedAt = DateTime.UtcNow;
    }

    public static LibraryItem Create(string title, Language language, string category,
        Guid uploadedById, string? description = null)
    {
        Guard.NotNullOrWhiteSpace(title, nameof(title));
        Guard.NotNull(language, nameof(language));
        Guard.NotNullOrWhiteSpace(category, nameof(category));
        Guard.NotDefault(uploadedById, nameof(uploadedById));
        var item = new LibraryItem(Guid.NewGuid(), title.Trim(), description?.Trim(),
            language, category.Trim(), uploadedById);
        item.RaiseDomainEvent(new LibraryItemAddedEvent(item.Id, title));
        return item;
    }

    public void Publish() => Status = ContentStatus.Published;
    public void Archive() => Status = ContentStatus.Archived;

    public void AddAttachment(MediaAttachment attachment)
    {
        Guard.NotNull(attachment, nameof(attachment));
        _attachments.Add(attachment);
    }
}
