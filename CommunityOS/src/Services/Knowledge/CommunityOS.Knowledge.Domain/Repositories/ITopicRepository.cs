using CommunityOS.Knowledge.Domain.Aggregates;

namespace CommunityOS.Knowledge.Domain.Repositories;

public interface ITopicRepository
{
    Task<Topic?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Topic>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Topic topic, CancellationToken ct = default);
    Task UpdateAsync(Topic topic, CancellationToken ct = default);
}