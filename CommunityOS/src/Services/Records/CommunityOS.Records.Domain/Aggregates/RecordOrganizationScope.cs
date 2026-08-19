using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Records.Domain.Aggregates;

/// <summary>
/// An additional organization-unit scope for a record (ADR-023). The primary
/// scope lives on <see cref="Record.OrganizationUnitId"/>; these rows add more
/// scopes. Access is granted when the caller holds the permission at any of the
/// record's scopes. Unit ids are references to the Organization read-model
/// projection — never FKs into the Organization database (ADR-016).
/// </summary>
public sealed class RecordOrganizationScope : Entity<Guid>
{
    private RecordOrganizationScope() : base(Guid.Empty)
    {
    }

    internal RecordOrganizationScope(Guid id, Guid organizationUnitId) : base(id)
    {
        OrganizationUnitId = organizationUnitId;
    }

    public Guid OrganizationUnitId { get; private set; }
}