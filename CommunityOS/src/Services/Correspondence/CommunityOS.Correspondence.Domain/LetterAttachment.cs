namespace CommunityOS.Correspondence.Domain;

/// <summary>
/// An attachment as a pure reference to an existing Documents artifact
/// (ADR-028 decision 1). Correspondence never stores document bytes or owns
/// version immutability.
/// </summary>
public sealed class LetterAttachment
{
    private LetterAttachment()
    {
    }

    public Guid Id { get; private set; }

    public Guid LetterId { get; private set; }

    public Guid DocumentId { get; private set; }

    /// <summary>Bounded reference type code (e.g. "enclosure").</summary>
    public string ReferenceType { get; private set; } = null!;

    public Guid AddedBy { get; private set; }

    public DateTime AddedOn { get; private set; }

    public static LetterAttachment Create(
        Guid letterId, Guid documentId, string referenceType, Guid addedBy, DateTime addedOn)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("Document id is required.", nameof(documentId));
        }

        if (string.IsNullOrWhiteSpace(referenceType))
        {
            throw new ArgumentException("Reference type is required.", nameof(referenceType));
        }

        return new LetterAttachment
        {
            Id = Guid.NewGuid(),
            LetterId = letterId,
            DocumentId = documentId,
            ReferenceType = referenceType.Trim(),
            AddedBy = addedBy,
            AddedOn = addedOn
        };
    }
}
