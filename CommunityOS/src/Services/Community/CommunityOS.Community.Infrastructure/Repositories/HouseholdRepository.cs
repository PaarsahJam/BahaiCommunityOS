using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Community.Infrastructure.Repositories;

public sealed class HouseholdRepository(CommunityDbContext db) : IHouseholdRepository
{
    public async Task<Household?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Households
            .Include(h => h.Members)
            .FirstOrDefaultAsync(h => h.Id == id, ct);

    public async Task<IReadOnlyList<Household>> ListAsync(CancellationToken ct = default) =>
        await db.Households
            .Include(h => h.Members)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Household>> ListByMemberAsync(
        Guid personId, CancellationToken ct = default) =>
        await db.Households
            .Include(h => h.Members)
            .Where(h => h.Members.Any(m => m.PersonId == personId))
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task AddAsync(Household household, CancellationToken ct = default)
    {
        await db.Households.AddAsync(household, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Household household, CancellationToken ct = default)
    {
        db.Households.Update(household);
        await db.SaveChangesAsync(ct);
    }
}
