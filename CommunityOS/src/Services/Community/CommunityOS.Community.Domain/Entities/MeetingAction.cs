using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.Entities;

/// <summary>
/// An action item arising from a meeting. Action items and minutes are
/// operational meeting information; formally declared records belong to the
/// future Records service.
/// </summary>
public sealed class MeetingAction : Entity<Guid>
{
    private MeetingAction() : base(Guid.Empty)
    {
        Description = null!;
    }

    private MeetingAction(
        Guid id,
        string description,
        Guid? assigneePersonId,
        DateTime? dueDate) : base(id)
    {
        Description = description;
        AssigneePersonId = assigneePersonId;
        DueDate = dueDate;
    }

    public string Description { get; private set; }
    public Guid? AssigneePersonId { get; private set; }
    public DateTime? DueDate { get; private set; }
    public bool IsCompleted { get; private set; }
    public DateTime? CompletedOn { get; private set; }

    public static MeetingAction Create(string description, Guid? assigneePersonId, DateTime? dueDate)
    {
        Guard.NotNullOrWhiteSpace(description, nameof(description));
        Guard.MaxLength(description, 2000, nameof(description));

        return new MeetingAction(
            Guid.NewGuid(),
            description.Trim(),
            assigneePersonId,
            dueDate?.ToUniversalTime());
    }

    public void Complete()
    {
        IsCompleted = true;
        CompletedOn = DateTime.UtcNow;
    }

    public void Reopen()
    {
        IsCompleted = false;
        CompletedOn = null;
    }
}
