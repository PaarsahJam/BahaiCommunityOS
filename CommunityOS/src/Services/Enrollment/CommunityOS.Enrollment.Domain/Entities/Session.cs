using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Enrollment.Domain.Entities;

public sealed class Session : Entity<Guid>
{
    public int SessionNumber { get; }
    public DateTime ScheduledDate { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public bool IsCompleted => CompletedAt.HasValue;

    private Session(Guid id, int sessionNumber, DateTime scheduledDate) : base(id)
    {
        SessionNumber = sessionNumber;
        ScheduledDate = scheduledDate;
    }

    public static Session Create(int sessionNumber, DateTime scheduledDate)
    {
        Guard.Positive(sessionNumber, nameof(sessionNumber));
        return new Session(Guid.NewGuid(), sessionNumber, scheduledDate);
    }

    public void Complete()
    {
        if (IsCompleted) return;
        CompletedAt = DateTime.UtcNow;
    }

    public void Reschedule(DateTime newDate) => ScheduledDate = newDate;
}
