using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Repositories;
using CommunityOS.Knowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Knowledge.Infrastructure.Repositories;

public sealed class EditionRepository(KnowledgeDbContext db) : IEditionRepository
{
    public async Task<Edition?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Editions.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<Edition>> ListByWorkAsync(
        Guid workId, CancellationToken ct = default) =>
        await db.Editions.AsNoTracking()
            .Where(e => e.WorkId == workId)
            .ToListAsync(ct);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        await db.Editions.AnyAsync(e => e.Id == id, ct);

    public async Task AddAsync(Edition edition, CancellationToken ct = default)
    {
        await db.Editions.AddAsync(edition, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Edition edition, CancellationToken ct = default)
    {
        db.Editions.Update(edition);
        await db.SaveChangesAsync(ct);
    }
}