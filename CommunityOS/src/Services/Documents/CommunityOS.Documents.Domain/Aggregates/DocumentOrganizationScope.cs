using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Documents.Domain.Aggregates;

/// <summary>
/// An additional organization-unit scope for a document (ADR-022). The primary
/// scope lives on <see cref="Document.OrganizationUnitId"/>; these rows add
/// more scopes. Access is granted when the caller holds the permission at any
/// of the document's scopes. Unit ids are references to the Organization
/// read-model projection — never FKs into the Organization database (ADR-016).
/// </summary>
public sealed class DocumentOrganizationScope : Entity<Guid>
{
    private DocumentOrganizationScope() : base(Guid.Empty)
    {
    }

    internal DocumentOrganizationScope(Guid id, Guid organizationUnitId) : base(id)
    {
        OrganizationUnitId = organizationUnitId;
    }

    public Guid OrganizationUnitId { get; private set; }
}