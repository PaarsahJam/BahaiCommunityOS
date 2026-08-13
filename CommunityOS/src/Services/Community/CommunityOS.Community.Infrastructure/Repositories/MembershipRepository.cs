using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Community.Infrastructure.Repositories;

public sealed class MembershipRepository(CommunityDbContext db) : IMembershipRepository
{
    public async Task<Membership?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Memberships
            .Include(m => m.PeriodHistory)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<Membership?> GetByPersonAsync(Guid personId, CancellationToken ct = default) =>
        await db.Memberships
            .Include(m => m.PeriodHistory)
            .FirstOrDefaultAsync(m => m.PersonId == personId, ct);

    public async Task<IReadOnlyList<Membership>> ListAsync(CancellationToken ct = default) =>
        await db.Memberships
            .Include(m => m.PeriodHistory)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task AddAsync(Membership membership, CancellationToken ct = default)
    {
        await db.Memberships.AddAsync(membership, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Membership membership, CancellationToken ct = default)
    {
        db.Memberships.Update(membership);
        await db.SaveChangesAsync(ct);
    }
}
