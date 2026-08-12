using CommunityOS.Organization.Domain.Aggregates;

namespace CommunityOS.Organization.Domain.Repositories;

public interface IInstitutionRepository
{
    Task<Institution?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Institution?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<Institution>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Institution institution, CancellationToken ct = default);
    Task UpdateAsync(Institution institution, CancellationToken ct = default);
}
