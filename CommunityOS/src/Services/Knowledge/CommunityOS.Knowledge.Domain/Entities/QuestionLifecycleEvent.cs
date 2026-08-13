using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Knowledge.Domain.Entities;

/// <summary>
/// An immutable record of a question's lifecycle transition
/// (Draft → Submitted → Published → Under Review → Merged | Archived).
/// </summary>
public sealed class QuestionLifecycleEvent : Entity<Guid>
{
    private QuestionLifecycleEvent() : base(Guid.Empty)
    {
        FromStatus = null!;
        ToStatus = null!;
    }

    private QuestionLifecycleEvent(
        Guid id,
        string fromStatus,
        string toStatus,
        Guid? actorId,
        DateTime occurredOn) : base(id)
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActorId = actorId;
        OccurredOn = occurredOn;
    }

    public string FromStatus { get; private set; }
    public string ToStatus { get; private set; }
    public Guid? ActorId { get; private set; }
    public DateTime OccurredOn { get; private set; }

    public static QuestionLifecycleEvent Create(
        string fromStatus,
        string toStatus,
        Guid? actorId,
        DateTime occurredOn) =>
        new(Guid.NewGuid(), fromStatus, toStatus, actorId, occurredOn);
}