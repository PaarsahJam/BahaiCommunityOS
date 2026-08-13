using CommunityOS.Community.Domain.Aggregates;

namespace CommunityOS.Community.Domain.Repositories;

public interface IHouseholdRepository
{
    Task<Household?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Household>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Household>> ListByMemberAsync(Guid personId, CancellationToken ct = default);
    Task AddAsync(Household household, CancellationToken ct = default);
    Task UpdateAsync(Household household, CancellationToken ct = default);
}
