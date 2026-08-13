using CommunityOS.Knowledge.Domain.Aggregates;

namespace CommunityOS.Knowledge.Domain.Repositories;

public interface IAnswerRepository
{
    Task<Answer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Answer>> ListByQuestionAsync(Guid questionId, CancellationToken ct = default);
    Task AddAsync(Answer answer, CancellationToken ct = default);
    Task UpdateAsync(Answer answer, CancellationToken ct = default);
}