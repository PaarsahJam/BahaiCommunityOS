namespace CommunityOS.Organization.Domain.Repositories;

using Organization = CommunityOS.Organization.Domain.Aggregates.Organization;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Organization?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<Organization>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Organization organization, CancellationToken ct = default);
    Task UpdateAsync(Organization organization, CancellationToken ct = default);
}
