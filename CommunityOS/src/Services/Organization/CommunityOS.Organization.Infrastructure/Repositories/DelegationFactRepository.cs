using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Repositories;
using CommunityOS.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Organization.Infrastructure.Repositories;

public sealed class DelegationFactRepository(OrganizationDbContext db) : IDelegationFactRepository
{
    public async Task<DelegationFact?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.DelegationFacts.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<DelegationFact>> ListByDelegatorAsync(
        Guid delegatorId, CancellationToken ct = default) =>
        await db.DelegationFacts
            .AsNoTracking()
            .Where(x => x.DelegatorId == delegatorId)
            .OrderBy(x => x.Period.EffectiveFrom)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DelegationFact>> ListByDelegateAsync(
        Guid delegateId, CancellationToken ct = default) =>
        await db.DelegationFacts
            .AsNoTracking()
            .Where(x => x.DelegateId == delegateId)
            .OrderBy(x => x.Period.EffectiveFrom)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DelegationFact>> ListByOrganizationUnitAsync(
        Guid organizationUnitId, CancellationToken ct = default) =>
        await db.DelegationFacts
            .AsNoTracking()
            .Where(x => x.OrganizationUnitId == organizationUnitId)
            .OrderBy(x => x.Period.EffectiveFrom)
            .ToListAsync(ct);

    public async Task AddAsync(DelegationFact delegationFact, CancellationToken ct = default)
    {
        await db.DelegationFacts.AddAsync(delegationFact, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(DelegationFact delegationFact, CancellationToken ct = default)
    {
        db.DelegationFacts.Update(delegationFact);
        await db.SaveChangesAsync(ct);
    }
}