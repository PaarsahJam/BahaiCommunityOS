using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Repositories;
using CommunityOS.Knowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Knowledge.Infrastructure.Repositories;

public sealed class AnswerRepository(KnowledgeDbContext db) : IAnswerRepository
{
    public async Task<Answer?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Answers
            .Include(a => a.Revisions)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Answer>> ListByQuestionAsync(
        Guid questionId, CancellationToken ct = default) =>
        await db.Answers
            .Include(a => a.Revisions)
            .AsNoTracking()
            .Where(a => a.QuestionId == questionId)
            .ToListAsync(ct);

    public async Task AddAsync(Answer answer, CancellationToken ct = default)
    {
        await db.Answers.AddAsync(answer, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Answer answer, CancellationToken ct = default)
    {
        db.Answers.Update(answer);
        await db.SaveChangesAsync(ct);
    }
}