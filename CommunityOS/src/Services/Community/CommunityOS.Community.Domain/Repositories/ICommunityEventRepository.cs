using CommunityOS.Community.Domain.Aggregates;

namespace CommunityOS.Community.Domain.Repositories;

public interface ICommunityEventRepository
{
    Task<CommunityEvent?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CommunityEvent>> ListAsync(
        DateTime? from = null,
        DateTime? through = null,
        Guid? organizationUnitId = null,
        CancellationToken ct = default);
    Task AddAsync(CommunityEvent communityEvent, CancellationToken ct = default);
    Task UpdateAsync(CommunityEvent communityEvent, CancellationToken ct = default);
}
