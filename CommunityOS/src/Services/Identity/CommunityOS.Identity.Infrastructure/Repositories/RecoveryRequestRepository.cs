using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Identity.Infrastructure.Repositories;

public sealed class RecoveryRequestRepository(IdentityDbContext db) : IRecoveryRequestRepository
{
    public async Task<RecoveryRequest?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.RecoveryRequests.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<RecoveryRequest?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        await db.RecoveryRequests.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<RecoveryRequest>> GetRecentByUserAsync(
        Guid userAccountId, int take = 20, CancellationToken ct = default) =>
        await db.RecoveryRequests
            .Where(x => x.UserAccountId == userAccountId)
            .OrderByDescending(x => x.RequestedOn)
            .Take(take)
            .ToListAsync(ct);

    public async Task AddAsync(RecoveryRequest request, CancellationToken ct = default)
    {
        await db.RecoveryRequests.AddAsync(request, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(RecoveryRequest request, CancellationToken ct = default)
    {
        db.RecoveryRequests.Update(request);
        await db.SaveChangesAsync(ct);
    }
}
