using CommunityOS.Knowledge.Domain.Aggregates;

namespace CommunityOS.Knowledge.Domain.Repositories;

public interface IPassageRepository
{
    Task<Passage?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Passage>> ListByEditionAsync(Guid editionId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Passage passage, CancellationToken ct = default);
    Task UpdateAsync(Passage passage, CancellationToken ct = default);
}