using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.Entities;

/// <summary>
/// A single agenda item on a meeting.
/// </summary>
public sealed class MeetingAgendaItem : Entity<Guid>
{
    private MeetingAgendaItem() : base(Guid.Empty)
    {
        Title = null!;
    }

    private MeetingAgendaItem(
        Guid id,
        string title,
        string? description,
        int order) : base(id)
    {
        Title = title;
        Description = description;
        Order = order;
    }

    public string Title { get; private set; }
    public string? Description { get; private set; }
    public int Order { get; private set; }
    public bool IsCompleted { get; private set; }

    public static MeetingAgendaItem Create(string title, string? description, int order)
    {
        Guard.NotNullOrWhiteSpace(title, nameof(title));
        Guard.MaxLength(title, 200, nameof(title));
        Guard.MaxLength(description ?? string.Empty, 2000, nameof(description));
        Guard.PositiveOrZero(order, nameof(order));

        return new MeetingAgendaItem(Guid.NewGuid(), title.Trim(), description?.Trim(), order);
    }

    public void Complete() => IsCompleted = true;

    public void Reopen() => IsCompleted = false;
}
