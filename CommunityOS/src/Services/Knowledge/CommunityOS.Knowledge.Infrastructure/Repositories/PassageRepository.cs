using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Repositories;
using CommunityOS.Knowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Knowledge.Infrastructure.Repositories;

public sealed class PassageRepository(KnowledgeDbContext db) : IPassageRepository
{
    public async Task<Passage?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Passages
            .Include(p => p.Revisions)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Passage>> ListByEditionAsync(
        Guid editionId, CancellationToken ct = default) =>
        await db.Passages
            .Include(p => p.Revisions)
            .AsNoTracking()
            .Where(p => p.EditionId == editionId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        await db.Passages.AnyAsync(p => p.Id == id, ct);

    public async Task AddAsync(Passage passage, CancellationToken ct = default)
    {
        await db.Passages.AddAsync(passage, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Passage passage, CancellationToken ct = default)
    {
        db.Passages.Update(passage);
        await db.SaveChangesAsync(ct);
    }
}