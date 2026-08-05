using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Community.Infrastructure.Repositories;

internal sealed class CommunityRepository(CommunityDbContext db) : ICommunityRepository
{
    public async Task<Community?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Communities
            .Include(c => c.LocalUnits)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Community>> GetByParentAsync(
        Guid parentId, CancellationToken ct = default) =>
        await db.Communities
            .Include(c => c.LocalUnits)
            .Where(c => c.ParentId == parentId)
            .ToListAsync(ct);

    public async Task AddAsync(Community community, CancellationToken ct = default)
    {
        await db.Communities.AddAsync(community, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Community community, CancellationToken ct = default)
    {
        db.Communities.Update(community);
        await db.SaveChangesAsync(ct);
    }
}
