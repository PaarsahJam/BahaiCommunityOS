using CommunityOS.Community.Domain.Aggregates;
using CommunityAggregate = CommunityOS.Community.Domain.Aggregates.Community;

namespace CommunityOS.Community.Domain.Repositories;

public interface ICommunityRepository
{
    Task<CommunityAggregate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CommunityAggregate>> GetByParentAsync(Guid parentId, CancellationToken ct = default);
    Task AddAsync(CommunityAggregate community, CancellationToken ct = default);
    Task UpdateAsync(CommunityAggregate community, CancellationToken ct = default);
}
