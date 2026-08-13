using CommunityOS.Knowledge.Domain.Entities;
using CommunityOS.Knowledge.Domain.Events;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Knowledge.Domain.Aggregates;

/// <summary>
/// A quotable unit of text within an edition (a paragraph, a tablet opening, a
/// section). Passages are immutable once published: text corrections create a
/// new revision record, never an in-place mutation. The current published text
/// is always the latest revision.
/// </summary>
public sealed class Passage : AggregateRoot<Guid>
{
    private readonly List<PassageRevision> _revisions = [];

    private Passage() : base(Guid.Empty)
    {
        ReferencePath = null!;
        Text = null!;
    }

    private Passage(
        Guid id,
        Guid editionId,
        string referencePath,
        string text,
        int sortOrder,
        PassageRevision initialRevision) : base(id)
    {
        EditionId = editionId;
        ReferencePath = referencePath;
        Text = text;
        SortOrder = sortOrder;
        _revisions.Add(initialRevision);
    }

    public Guid EditionId { get; private set; }
    public string ReferencePath { get; private set; }
    public string Text { get; private set; }
    public int SortOrder { get; private set; }
    public int Revision => _revisions.Count;

    public IReadOnlyList<PassageRevision> Revisions => _revisions.AsReadOnly();

    public static Passage Import(Guid editionId, string referencePath, string text, int sortOrder)
    {
        Guard.NotDefault(editionId, nameof(editionId));
        Guard.NotNullOrWhiteSpace(referencePath, nameof(referencePath));
        Guard.MaxLength(referencePath, 500, nameof(referencePath));
        Guard.NotNullOrWhiteSpace(text, nameof(text));
        Guard.PositiveOrZero(sortOrder, nameof(sortOrder));

        var passage = new Passage(
            Guid.NewGuid(),
            editionId,
            referencePath.Trim(),
            text.Trim(),
            sortOrder,
            PassageRevision.Initial(text.Trim()));

        passage.RaiseDomainEvent(new PassageImportedEvent(
            passage.Id, passage.EditionId, passage.ReferencePath));
        return passage;
    }

    /// <summary>
    /// Publishes a correction as a new revision. The passage text is immutable —
    /// the current text is always the latest revision and is never overwritten.
    /// </summary>
    public void Correct(string text)
    {
        Guard.NotNullOrWhiteSpace(text, nameof(text));

        Text = text.Trim();
        _revisions.Add(PassageRevision.Of(_revisions.Count + 1, text.Trim()));

        RaiseDomainEvent(new PassageCorrectedEvent(Id, EditionId, Revision));
    }
}