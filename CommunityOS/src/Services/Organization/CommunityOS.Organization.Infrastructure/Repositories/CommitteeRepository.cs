using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Repositories;
using CommunityOS.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Organization.Infrastructure.Repositories;

public sealed class CommitteeRepository(OrganizationDbContext db) : ICommitteeRepository
{
    public async Task<Committee?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Committees
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<Committee>> ListByOrganizationAsync(
        Guid organizationId, CancellationToken ct = default) =>
        await db.Committees
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Committee>> ListAsync(CancellationToken ct = default) =>
        await db.Committees
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task AddAsync(Committee committee, CancellationToken ct = default)
    {
        await db.Committees.AddAsync(committee, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Committee committee, CancellationToken ct = default)
    {
        db.Committees.Update(committee);
        await db.SaveChangesAsync(ct);
    }
}