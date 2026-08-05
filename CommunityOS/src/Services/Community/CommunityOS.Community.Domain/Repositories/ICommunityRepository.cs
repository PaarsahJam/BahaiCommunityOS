using CommunityOS.Community.Domain.Aggregates;

namespace CommunityOS.Community.Domain.Repositories;

public interface ICommunityRepository
{
    Task<Community?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Community>> GetByParentAsync(Guid parentId, CancellationToken ct = default);
    Task AddAsync(Community community, CancellationToken ct = default);
    Task UpdateAsync(Community community, CancellationToken ct = default);
}
