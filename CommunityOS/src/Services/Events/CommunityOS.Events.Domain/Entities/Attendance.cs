using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Events.Domain.Entities;

public sealed class Attendance : Entity<Guid>
{
    public Guid MemberId { get; }
    public DateTime RecordedAt { get; }
    public bool IsConfirmed { get; private set; }

    private Attendance(Guid id, Guid memberId) : base(id)
    {
        MemberId = memberId;
        RecordedAt = DateTime.UtcNow;
        IsConfirmed = false;
    }

    public static Attendance Record(Guid memberId)
    {
        Guard.NotDefault(memberId, nameof(memberId));
        return new Attendance(Guid.NewGuid(), memberId);
    }

    public void Confirm() => IsConfirmed = true;
}
