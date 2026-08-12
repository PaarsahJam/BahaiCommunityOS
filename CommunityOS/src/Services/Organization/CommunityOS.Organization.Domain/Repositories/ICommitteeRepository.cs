using CommunityOS.Organization.Domain.Aggregates;

namespace CommunityOS.Organization.Domain.Repositories;

public interface ICommitteeRepository
{
    Task<Committee?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Committee>> ListByOrganizationAsync(
        Guid organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Committee>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Committee committee, CancellationToken ct = default);
    Task UpdateAsync(Committee committee, CancellationToken ct = default);
}
