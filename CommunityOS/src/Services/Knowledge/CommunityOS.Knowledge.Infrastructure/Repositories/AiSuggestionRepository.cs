using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Repositories;
using CommunityOS.Knowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Knowledge.Infrastructure.Repositories;

public sealed class AiSuggestionRepository(KnowledgeDbContext db) : IAiSuggestionRepository
{
    public async Task<AiSuggestion?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.AiSuggestions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<AiSuggestion>> ListByQuestionAsync(
        Guid questionId, CancellationToken ct = default) =>
        await db.AiSuggestions.AsNoTracking()
            .Where(s => s.QuestionId == questionId)
            .ToListAsync(ct);

    public async Task AddAsync(AiSuggestion suggestion, CancellationToken ct = default)
    {
        await db.AiSuggestions.AddAsync(suggestion, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AiSuggestion suggestion, CancellationToken ct = default)
    {
        db.AiSuggestions.Update(suggestion);
        await db.SaveChangesAsync(ct);
    }
}