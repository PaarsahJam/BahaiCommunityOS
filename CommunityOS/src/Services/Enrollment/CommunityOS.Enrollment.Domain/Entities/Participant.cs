using CommunityOS.Enrollment.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Enrollment.Domain.Entities;

public sealed class Participant : Entity<Guid>
{
    public Guid MemberId { get; }
    public ProgressStatus Status { get; private set; }
    public DateTime EnrolledAt { get; }
    public DateTime? CompletedAt { get; private set; }

    private Participant(Guid id, Guid memberId) : base(id)
    {
        MemberId = memberId;
        Status = ProgressStatus.NotStarted;
        EnrolledAt = DateTime.UtcNow;
    }

    public static Participant Enroll(Guid memberId)
    {
        Guard.NotDefault(memberId, nameof(memberId));
        return new Participant(Guid.NewGuid(), memberId);
    }

    public void Start() => Status = ProgressStatus.InProgress;

    public void Complete()
    {
        Status = ProgressStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    public void Withdraw() => Status = ProgressStatus.Withdrawn;
}
