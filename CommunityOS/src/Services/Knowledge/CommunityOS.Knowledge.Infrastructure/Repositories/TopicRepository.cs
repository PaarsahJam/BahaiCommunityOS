using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Repositories;
using CommunityOS.Knowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Knowledge.Infrastructure.Repositories;

public sealed class TopicRepository(KnowledgeDbContext db) : ITopicRepository
{
    public async Task<Topic?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Topics.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<Topic>> ListAsync(CancellationToken ct = default) =>
        await db.Topics.AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(Topic topic, CancellationToken ct = default)
    {
        await db.Topics.AddAsync(topic, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Topic topic, CancellationToken ct = default)
    {
        db.Topics.Update(topic);
        await db.SaveChangesAsync(ct);
    }
}