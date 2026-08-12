using CommunityOS.Organization.Domain.Aggregates;

namespace CommunityOS.Organization.Domain.Repositories;

public interface IOrganizationUnitRepository
{
    Task<OrganizationUnit?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationUnit>> ListByOrganizationAsync(
        Guid organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationUnit>> ListChildrenAsync(
        Guid parentId, DateTime? asOf = null, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationUnit>> ListRootsAsync(
        Guid organizationId, DateTime? asOf = null, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> ListAncestorIdsAsync(
        Guid organizationUnitId, DateTime? asOf = null, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationUnit>> ListDescendantsAsync(
        Guid organizationUnitId, DateTime? asOf = null, CancellationToken ct = default);

    /// <summary>
    /// True when <paramref name="candidateDescendantId"/> is inside the subtree
    /// rooted at <paramref name="ancestorId"/> as of <paramref name="asOf"/>.
    /// Used for cycle detection when reparenting.
    /// </summary>
    Task<bool> IsDescendantAsync(
        Guid candidateDescendantId,
        Guid ancestorId,
        DateTime? asOf = null,
        CancellationToken ct = default);

    Task AddAsync(OrganizationUnit unit, CancellationToken ct = default);
    Task UpdateAsync(OrganizationUnit unit, CancellationToken ct = default);
}
