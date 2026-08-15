using CommunityOS.Documents.Domain.Aggregates;

namespace CommunityOS.Documents.Domain.Repositories;

public interface IOrganizationUnitReferenceRepository
{
    Task<OrganizationUnitReference?> GetUnitByIdAsync(
        Guid organizationUnitId, CancellationToken ct = default);

    Task UpsertUnitAsync(OrganizationUnitReference reference, CancellationToken ct = default);
}