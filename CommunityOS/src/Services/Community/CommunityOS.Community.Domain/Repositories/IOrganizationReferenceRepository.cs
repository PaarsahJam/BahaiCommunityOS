using CommunityOS.Community.Domain.Aggregates;

namespace CommunityOS.Community.Domain.Repositories;

public interface IOrganizationReferenceRepository
{
    Task<OrganizationReference?> GetByIdAsync(Guid organizationId, CancellationToken ct = default);
    Task UpsertAsync(OrganizationReference reference, CancellationToken ct = default);
    Task<OrganizationUnitReference?> GetUnitByIdAsync(Guid organizationUnitId, CancellationToken ct = default);
    Task UpsertUnitAsync(OrganizationUnitReference reference, CancellationToken ct = default);
}
