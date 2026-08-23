namespace CommunityOS.Correspondence.Domain;

/// <summary>
/// Materialization correlation state: the Documents artifact that carries the
/// immutable submitted-letter version (ADR-028 decision 7). One link per
/// document id; the correlation is filtered strictly to letters by source
/// context when the gated consumer is eventually registered.
/// </summary>
public sealed class LetterDocumentLink
{
    private LetterDocumentLink()
    {
    }

    public Guid Id { get; private set; }

    public Guid LetterId { get; private set; }

    public Guid DocumentId { get; private set; }

    public int VersionNumber { get; private set; }

    public string ContentHash { get; private set; } = null!;

    public DateTime MaterializedOn { get; private set; }

    public static LetterDocumentLink Create(
        Guid letterId, Guid documentId, int versionNumber, string contentHash, DateTime materializedOn) =>
        new()
        {
            Id = Guid.NewGuid(),
            LetterId = letterId,
            DocumentId = documentId,
            VersionNumber = versionNumber,
            ContentHash = contentHash,
            MaterializedOn = materializedOn
        };
}
