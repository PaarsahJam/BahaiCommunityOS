using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Repositories;
using CommunityOS.Knowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Knowledge.Infrastructure.Repositories;

public sealed class DiscussionRepository(KnowledgeDbContext db) : IDiscussionRepository
{
    public async Task<Discussion?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Discussions
            .Include(d => d.Comments)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<Discussion>> ListByQuestionAsync(
        Guid questionId, CancellationToken ct = default) =>
        await db.Discussions
            .Include(d => d.Comments)
            .AsNoTracking()
            .Where(d => d.QuestionId == questionId)
            .ToListAsync(ct);

    public async Task AddAsync(Discussion discussion, CancellationToken ct = default)
    {
        await db.Discussions.AddAsync(discussion, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Discussion discussion, CancellationToken ct = default)
    {
        db.Discussions.Update(discussion);
        await db.SaveChangesAsync(ct);
    }
}