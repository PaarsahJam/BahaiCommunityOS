using CommunityOS.Knowledge.Domain.Aggregates;

namespace CommunityOS.Knowledge.Domain.Repositories;

public interface IEditionRepository
{
    Task<Edition?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Edition>> ListByWorkAsync(Guid workId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Edition edition, CancellationToken ct = default);
    Task UpdateAsync(Edition edition, CancellationToken ct = default);
}