using CommunityOS.Authorization.Domain.Aggregates;

namespace CommunityOS.Authorization.Domain.Repositories;

public interface IDelegationRepository
{
    Task<Delegation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Delegation>> ListByDelegateAsync(Guid delegateId, CancellationToken ct = default);
    Task<IReadOnlyList<Delegation>> ListByDelegatorAsync(Guid delegatorId, CancellationToken ct = default);
    Task AddAsync(Delegation delegation, CancellationToken ct = default);
    Task UpdateAsync(Delegation delegation, CancellationToken ct = default);
}
