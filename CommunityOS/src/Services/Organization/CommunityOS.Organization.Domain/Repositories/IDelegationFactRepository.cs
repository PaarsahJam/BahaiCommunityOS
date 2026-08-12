using CommunityOS.Organization.Domain.Aggregates;

namespace CommunityOS.Organization.Domain.Repositories;

public interface IDelegationFactRepository
{
    Task<DelegationFact?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DelegationFact>> ListByDelegatorAsync(
        Guid delegatorId, CancellationToken ct = default);
    Task<IReadOnlyList<DelegationFact>> ListByDelegateAsync(
        Guid delegateId, CancellationToken ct = default);
    Task<IReadOnlyList<DelegationFact>> ListByOrganizationUnitAsync(
        Guid organizationUnitId, CancellationToken ct = default);
    Task AddAsync(DelegationFact delegationFact, CancellationToken ct = default);
    Task UpdateAsync(DelegationFact delegationFact, CancellationToken ct = default);
}
