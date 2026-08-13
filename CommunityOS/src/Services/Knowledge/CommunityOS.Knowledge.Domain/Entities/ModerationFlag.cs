using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Knowledge.Domain.Entities;

/// <summary>
/// A user flag attached to a question, gating promotion through moderation.
/// </summary>
public sealed class ModerationFlag : Entity<Guid>
{
    private ModerationFlag() : base(Guid.Empty)
    {
        Reason = null!;
    }

    private ModerationFlag(Guid id, string reason, Guid? flaggedBy, DateTime flaggedOn) : base(id)
    {
        Reason = reason;
        FlaggedBy = flaggedBy;
        FlaggedOn = flaggedOn;
    }

    public string Reason { get; private set; }
    public Guid? FlaggedBy { get; private set; }
    public DateTime FlaggedOn { get; private set; }

    public static ModerationFlag Create(string reason, Guid? flaggedBy, DateTime flaggedOn) =>
        new(Guid.NewGuid(), reason, flaggedBy, flaggedOn);
}