using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Repositories;
using CommunityOS.Knowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Knowledge.Infrastructure.Repositories;

public sealed class WorkRepository(KnowledgeDbContext db) : IWorkRepository
{
    public async Task<Work?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Works.FirstOrDefaultAsync(w => w.Id == id, ct);

    public async Task<IReadOnlyList<Work>> ListAsync(CancellationToken ct = default) =>
        await db.Works.AsNoTracking().ToListAsync(ct);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        await db.Works.AnyAsync(w => w.Id == id, ct);

    public async Task AddAsync(Work work, CancellationToken ct = default)
    {
        await db.Works.AddAsync(work, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Work work, CancellationToken ct = default)
    {
        db.Works.Update(work);
        await db.SaveChangesAsync(ct);
    }
}