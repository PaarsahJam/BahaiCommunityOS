using CommunityOS.Knowledge.Domain.Aggregates;

namespace CommunityOS.Knowledge.Domain.Repositories;

public interface IAiSuggestionRepository
{
    Task<AiSuggestion?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AiSuggestion>> ListByQuestionAsync(Guid questionId, CancellationToken ct = default);
    Task AddAsync(AiSuggestion suggestion, CancellationToken ct = default);
    Task UpdateAsync(AiSuggestion suggestion, CancellationToken ct = default);
}