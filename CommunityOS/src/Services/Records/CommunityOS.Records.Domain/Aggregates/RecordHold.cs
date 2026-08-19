using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Records.Domain.Aggregates;

/// <summary>
/// A document-level reference carried by a hold (ADR-023). When a hold targets
/// a document, Records writes the hold reference onto the document through the
/// Documents classify surface so Documents enforces its own deactivation-protection
/// rule. These references are intentionally never exported in integration events
/// — consumers resolve document coverage through the Records API.
/// </summary>
public sealed class RecordHoldDocumentReference : Entity<Guid>
{
    private RecordHoldDocumentReference() : base(Guid.Empty)
    {
    }

    internal RecordHoldDocumentReference(Guid id, Guid documentId, int? versionNumber) : base(id)
    {
        DocumentId = documentId;
        VersionNumber = versionNumber;
    }

    /// <summary>Creates a document reference carried by a hold.</summary>
    public static RecordHoldDocumentReference Create(Guid documentId, int? versionNumber = null) =>
        new(Guid.NewGuid(), documentId, versionNumber);

    public Guid DocumentId { get; private set; }

    public int? VersionNumber { get; private set; }
}

/// <summary>
/// A legal or administrative hold owned by Records (ADR-023). While active, a
/// hold freezes disposition: the target record cannot be deactivated and
/// referenced held documents cannot be deactivated (Documents honors the
/// reference). Release requires a subject different from the placer. The reason
/// is sensitive — never exported in events or logs.
/// </summary>
public sealed class RecordHold : Entity<Guid>
{
    private readonly List<RecordHoldDocumentReference> _documentReferences = [];

    private RecordHold() : base(Guid.Empty)
    {
        HoldType = null!;
        Reason = null!;
    }

    internal RecordHold(
        Guid id,
        string holdType,
        string reason,
        Guid placedBy,
        DateTime placedOn,
        IReadOnlyList<RecordHoldDocumentReference>? documentReferences) : base(id)
    {
        HoldType = holdType;
        Reason = reason;
        PlacedBy = placedBy;
        PlacedOn = placedOn;
        if (documentReferences is not null)
            _documentReferences.AddRange(documentReferences);
    }

    public string HoldType { get; private set; }

    /// <summary>Sensitive free-text reason. Never exported in events or logs.</summary>
    public string Reason { get; private set; }

    public Guid PlacedBy { get; private set; }

    public DateTime PlacedOn { get; private set; }

    public Guid? ReleasedBy { get; private set; }

    public DateTime? ReleasedOn { get; private set; }

    public IReadOnlyList<RecordHoldDocumentReference> DocumentReferences => _documentReferences.AsReadOnly();

    public bool IsActive => ReleasedOn is null;

    /// <summary>Releases the hold. The releaser must differ from the placer (separation of duties).</summary>
    public void Release(Guid releasedBy, DateTime releasedOn)
    {
        if (releasedBy == PlacedBy)
            throw new Exceptions.HoldReleaseByPlacerException(Id);

        ReleasedBy = releasedBy;
        ReleasedOn = releasedOn.ToUniversalTime();
    }
}