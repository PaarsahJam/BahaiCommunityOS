using CommunityOS.Community.Domain.Aggregates;

namespace CommunityOS.Community.Domain.Repositories;

public interface IMembershipRepository
{
    Task<Membership?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Membership?> GetByPersonAsync(Guid personId, CancellationToken ct = default);
    Task<IReadOnlyList<Membership>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Membership membership, CancellationToken ct = default);
    Task UpdateAsync(Membership membership, CancellationToken ct = default);
}
