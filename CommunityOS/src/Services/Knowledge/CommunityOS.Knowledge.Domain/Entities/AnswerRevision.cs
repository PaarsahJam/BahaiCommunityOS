using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Knowledge.Domain.Entities;

/// <summary>
/// An immutable revision of an answer's body. Answers are edited by appending a
/// new revision; the current body is always the latest revision.
/// </summary>
public sealed class AnswerRevision : Entity<Guid>
{
    private AnswerRevision() : base(Guid.Empty)
    {
        Body = null!;
    }

    private AnswerRevision(Guid id, int revision, string body, DateTime revisedOn) : base(id)
    {
        Revision = revision;
        Body = body;
        RevisedOn = revisedOn;
    }

    public int Revision { get; private set; }
    public string Body { get; private set; }
    public DateTime RevisedOn { get; private set; }

    public static AnswerRevision Initial(string body) =>
        new(Guid.NewGuid(), 1, body, DateTime.UtcNow);

    public static AnswerRevision Of(int revision, string body) =>
        new(Guid.NewGuid(), revision, body, DateTime.UtcNow);
}