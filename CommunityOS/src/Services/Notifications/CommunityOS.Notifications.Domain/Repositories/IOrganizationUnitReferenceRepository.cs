using CommunityOS.Notifications.Domain.Aggregates;

namespace CommunityOS.Notifications.Domain.Repositories;

public interface IOrganizationUnitReferenceRepository
{
    Task<OrganizationUnitReference?> GetByOrganizationUnitIdAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);

    Task<List<OrganizationUnitReference>> ListAsync(CancellationToken cancellationToken = default);

    Task AddAsync(OrganizationUnitReference reference, CancellationToken cancellationToken = default);
}