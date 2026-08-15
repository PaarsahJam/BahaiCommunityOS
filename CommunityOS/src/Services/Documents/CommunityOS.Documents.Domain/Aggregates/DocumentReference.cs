using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Documents.Domain.Aggregates;

/// <summary>
/// The attachment/evidence mechanism used by other bounded contexts (Workflow,
/// Records, Correspondence, Finance, Tickets) to reference a document without
/// owning storage (ADR-022). The lifecycle of the referencing entity stays with
/// its owning context; this is a Documents-side convenience for enumerating
/// what a document is attached to. Unique per
/// <c>(SourceContext, SourceEntityId, DocumentId, ReferenceType)</c>.
/// </summary>
public sealed class DocumentReference : Entity<Guid>
{
    private DocumentReference() : base(Guid.Empty)
    {
        SourceContext = null!;
        ReferenceType = null!;
    }

    internal DocumentReference(
        Guid id,
        string sourceContext,
        Guid sourceEntityId,
        string referenceType,
        Guid createdBy,
        DateTime createdOn) : base(id)
    {
        SourceContext = sourceContext;
        SourceEntityId = sourceEntityId;
        ReferenceType = referenceType;
        CreatedBy = createdBy;
        CreatedOn = createdOn;
    }

    public string SourceContext { get; private set; }

    public Guid SourceEntityId { get; private set; }

    public string ReferenceType { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedOn { get; private set; }
}