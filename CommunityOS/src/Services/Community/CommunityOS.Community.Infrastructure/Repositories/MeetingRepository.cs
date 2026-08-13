using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Community.Infrastructure.Repositories;

public sealed class MeetingRepository(CommunityDbContext db) : IMeetingRepository
{
    public async Task<Meeting?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Meetings
            .Include(m => m.Participants)
            .Include(m => m.AgendaItems)
            .Include(m => m.Actions)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<IReadOnlyList<Meeting>> ListAsync(
        DateTime? from = null,
        DateTime? through = null,
        Guid? organizationUnitId = null,
        CancellationToken ct = default)
    {
        var query = db.Meetings.AsQueryable();

        if (organizationUnitId is { } orgUnitId)
            query = query.Where(m => m.OrganizationUnitId == orgUnitId);
        if (from is { } fromValue)
            query = query.Where(m => m.EndsAt == null || m.EndsAt >= fromValue);
        if (through is { } throughValue)
            query = query.Where(m => m.StartsAt <= throughValue);

        return await query.AsNoTracking().ToListAsync(ct);
    }

    public async Task AddAsync(Meeting meeting, CancellationToken ct = default)
    {
        await db.Meetings.AddAsync(meeting, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Meeting meeting, CancellationToken ct = default)
    {
        db.Meetings.Update(meeting);
        await db.SaveChangesAsync(ct);
    }
}
