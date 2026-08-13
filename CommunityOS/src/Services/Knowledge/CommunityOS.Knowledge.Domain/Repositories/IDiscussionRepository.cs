using CommunityOS.Knowledge.Domain.Aggregates;

namespace CommunityOS.Knowledge.Domain.Repositories;

public interface IDiscussionRepository
{
    Task<Discussion?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Discussion>> ListByQuestionAsync(Guid questionId, CancellationToken ct = default);
    Task AddAsync(Discussion discussion, CancellationToken ct = default);
    Task UpdateAsync(Discussion discussion, CancellationToken ct = default);
}