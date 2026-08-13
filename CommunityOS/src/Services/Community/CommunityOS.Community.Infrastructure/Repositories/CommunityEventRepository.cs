using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Community.Infrastructure.Repositories;

public sealed class CommunityEventRepository(CommunityDbContext db)
    : ICommunityEventRepository
{
    public async Task<CommunityEvent?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.CommunityEvents.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<CommunityEvent>> ListAsync(
        DateTime? from = null,
        DateTime? through = null,
        Guid? organizationUnitId = null,
        CancellationToken ct = default)
    {
        var query = db.CommunityEvents.AsQueryable();

        if (organizationUnitId is { } orgUnitId)
            query = query.Where(e => e.OrganizationUnitId == orgUnitId);
        if (from is { } fromValue)
            query = query.Where(e => e.EndsAt == null || e.EndsAt >= fromValue);
        if (through is { } throughValue)
            query = query.Where(e => e.StartsAt <= throughValue);

        return await query.AsNoTracking().ToListAsync(ct);
    }

    public async Task AddAsync(CommunityEvent communityEvent, CancellationToken ct = default)
    {
        await db.CommunityEvents.AddAsync(communityEvent, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(CommunityEvent communityEvent, CancellationToken ct = default)
    {
        db.CommunityEvents.Update(communityEvent);
        await db.SaveChangesAsync(ct);
    }
}
