using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Community.Infrastructure.Repositories;

public sealed class ActivityRepository(CommunityDbContext db) : IActivityRepository
{
    public async Task<Activity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Activities.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Activity>> ListAsync(
        DateTime? from = null,
        DateTime? through = null,
        Guid? organizationUnitId = null,
        CancellationToken ct = default)
    {
        var query = db.Activities.AsQueryable();

        if (organizationUnitId is { } orgUnitId)
            query = query.Where(a => a.OrganizationUnitId == orgUnitId);
        if (from is { } fromValue)
            query = query.Where(a => a.Schedule.EndsAt == null || a.Schedule.EndsAt >= fromValue);
        if (through is { } throughValue)
            query = query.Where(a => a.Schedule.StartsAt <= throughValue);

        return await query.AsNoTracking().ToListAsync(ct);
    }

    public async Task AddAsync(Activity activity, CancellationToken ct = default)
    {
        await db.Activities.AddAsync(activity, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Activity activity, CancellationToken ct = default)
    {
        db.Activities.Update(activity);
        await db.SaveChangesAsync(ct);
    }
}
