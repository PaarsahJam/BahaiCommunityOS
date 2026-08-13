using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Repositories;
using CommunityOS.Knowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Knowledge.Infrastructure.Repositories;

public sealed class QuestionRepository(KnowledgeDbContext db) : IQuestionRepository
{
    public async Task<Question?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Questions
            .Include(q => q.Tags)
            .Include(q => q.ModerationFlags)
            .Include(q => q.LifecycleEvents)
            .FirstOrDefaultAsync(q => q.Id == id, ct);

    public async Task<IReadOnlyList<Question>> ListAsync(CancellationToken ct = default) =>
        await db.Questions
            .Include(q => q.Tags)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Question>> ListByOrganizationUnitAsync(
        Guid organizationUnitId, CancellationToken ct = default) =>
        await db.Questions
            .Include(q => q.Tags)
            .AsNoTracking()
            .Where(q => q.OrganizationUnitId == organizationUnitId)
            .ToListAsync(ct);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        await db.Questions.AnyAsync(q => q.Id == id, ct);

    public async Task AddAsync(Question question, CancellationToken ct = default)
    {
        await db.Questions.AddAsync(question, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Question question, CancellationToken ct = default)
    {
        db.Questions.Update(question);
        await db.SaveChangesAsync(ct);
    }
}