using CommunityOS.Knowledge.Domain.Aggregates;

namespace CommunityOS.Knowledge.Domain.Repositories;

public interface IWorkRepository
{
    Task<Work?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Work>> ListAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Work work, CancellationToken ct = default);
    Task UpdateAsync(Work work, CancellationToken ct = default);
}