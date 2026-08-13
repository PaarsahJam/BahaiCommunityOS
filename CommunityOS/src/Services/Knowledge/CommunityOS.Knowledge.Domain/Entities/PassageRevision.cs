using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Knowledge.Domain.Entities;

/// <summary>
/// An immutable revision record of a passage's published text. Passages are
/// never mutated in place; a correction appends a new revision and the passage
/// current text is always the latest revision.
/// </summary>
public sealed class PassageRevision : Entity<Guid>
{
    private PassageRevision() : base(Guid.Empty)
    {
        Text = null!;
    }

    private PassageRevision(Guid id, int revision, string text, DateTime correctedOn) : base(id)
    {
        Revision = revision;
        Text = text;
        CorrectedOn = correctedOn;
    }

    public int Revision { get; private set; }
    public string Text { get; private set; }
    public DateTime CorrectedOn { get; private set; }

    public static PassageRevision Initial(string text) => new(
        Guid.NewGuid(), 1, text, DateTime.UtcNow);

    public static PassageRevision Of(int revision, string text) => new(
        Guid.NewGuid(), revision, text, DateTime.UtcNow);
}