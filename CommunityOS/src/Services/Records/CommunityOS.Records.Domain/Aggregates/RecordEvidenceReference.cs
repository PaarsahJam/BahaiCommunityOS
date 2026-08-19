using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Records.Domain.Aggregates;

/// <summary>
/// The Records-side link binding a record to a specific document version used
/// as evidence (ADR-023). The Documents-side mirror is <c>DocumentReference</c>
/// with <c>SourceContext = records.record</c>. Records does not read the
/// Documents database; evidence is reconciled through
/// <c>DocumentDeactivated</c>/<c>DocumentRestored</c>. Unique per
/// <c>(RecordId, DocumentId, VersionNumber, ReferenceType)</c>.
/// </summary>
public sealed class RecordEvidenceReference : Entity<Guid>
{
    private RecordEvidenceReference() : base(Guid.Empty)
    {
        ReferenceType = null!;
    }

    internal RecordEvidenceReference(
        Guid id,
        Guid documentId,
        int versionNumber,
        string referenceType,
        Guid attachedBy,
        DateTime attachedOn) : base(id)
    {
        DocumentId = documentId;
        VersionNumber = versionNumber;
        ReferenceType = referenceType;
        AttachedBy = attachedBy;
        AttachedOn = attachedOn;
    }

    public Guid DocumentId { get; private set; }

    public int VersionNumber { get; private set; }

    public string ReferenceType { get; private set; }

    public Guid AttachedBy { get; private set; }

    public DateTime AttachedOn { get; private set; }

    /// <summary>
    /// Set when the referenced document is deactivated, flagging the evidence
    /// for reconciliation (runbook: re-verify the document or attach
    /// alternative evidence). Cleared on <c>DocumentRestored</c>.
    /// </summary>
    public DateTime? DocumentDeactivatedOn { get; private set; }

    /// <summary>Set when the referenced document is restored.</summary>
    public DateTime? DocumentRestoredOn { get; private set; }

    /// <summary>Flags this evidence reference because its document was deactivated.</summary>
    public void MarkDocumentDeactivated(DateTime deactivatedOn) =>
        DocumentDeactivatedOn = deactivatedOn.ToUniversalTime();

    /// <summary>Clears the deactivation flag because the document was restored.</summary>
    public void MarkDocumentRestored(DateTime restoredOn) =>
        DocumentRestoredOn = restoredOn.ToUniversalTime();
}