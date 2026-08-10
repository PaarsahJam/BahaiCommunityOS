using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Repositories;
using CommunityOS.Authorization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Authorization.Infrastructure.Repositories;

public sealed class DelegationRepository(AuthorizationDbContext db) : IDelegationRepository
{
    public async Task<Delegation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Delegations.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<Delegation>> ListByDelegateAsync(
        Guid delegateId, CancellationToken ct = default) =>
        await db.Delegations
            .Where(x => x.DelegateId == delegateId)
            .OrderBy(x => x.ExpiresOn)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Delegation>> ListByDelegatorAsync(
        Guid delegatorId, CancellationToken ct = default) =>
        await db.Delegations
            .Where(x => x.DelegatorId == delegatorId)
            .OrderBy(x => x.ExpiresOn)
            .ToListAsync(ct);

    public async Task AddAsync(Delegation delegation, CancellationToken ct = default)
    {
        await db.Delegations.AddAsync(delegation, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Delegation delegation, CancellationToken ct = default)
    {
        db.Delegations.Update(delegation);
        await db.SaveChangesAsync(ct);
    }
}
