using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Workflow.Domain.Aggregates;

/// <summary>
/// Read-model projection of an Organization unit, maintained by the
/// Organization integration consumer (ADR-024, ADR-016). Workflow never reads
/// the Organization database; it only holds the minimal reference needed for
/// scope checks and access decisions. The projection is keyed by the stable
/// unit id and stores no PII.
/// </summary>
public sealed class OrganizationUnitReference : AggregateRoot<Guid>
{
    private OrganizationUnitReference() : base(Guid.Empty)
    {
    }

    internal OrganizationUnitReference(Guid id, Guid organizationUnitId, DateTime createdOn) : base(id)
    {
        OrganizationUnitId = organizationUnitId;
        CreatedOn = createdOn.ToUniversalTime();
    }

    public Guid OrganizationUnitId { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public static OrganizationUnitReference Create(Guid organizationUnitId, DateTime createdOn) =>
        new(Guid.NewGuid(), organizationUnitId, createdOn.ToUniversalTime());
}