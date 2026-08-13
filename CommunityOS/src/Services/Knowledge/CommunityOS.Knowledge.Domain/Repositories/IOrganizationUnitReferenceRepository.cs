using CommunityOS.Knowledge.Domain.Aggregates;

namespace CommunityOS.Knowledge.Domain.Repositories;

public interface IOrganizationUnitReferenceRepository
{
    Task<OrganizationUnitReference?> GetUnitByIdAsync(Guid organizationUnitId, CancellationToken ct = default);
    Task UpsertUnitAsync(OrganizationUnitReference reference, CancellationToken ct = default);
}