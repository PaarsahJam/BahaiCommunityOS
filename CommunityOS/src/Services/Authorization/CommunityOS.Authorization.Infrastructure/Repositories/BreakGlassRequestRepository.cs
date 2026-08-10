using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Repositories;
using CommunityOS.Authorization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Authorization.Infrastructure.Repositories;

public sealed class BreakGlassRequestRepository(AuthorizationDbContext db)
    : IBreakGlassRequestRepository
{
    public async Task<BreakGlassRequest?> GetByIdAsync(
        Guid id, CancellationToken ct = default) =>
        await db.BreakGlassRequests.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<BreakGlassRequest>> ListByRequesterAsync(
        Guid requesterId, CancellationToken ct = default) =>
        await db.BreakGlassRequests
            .Where(x => x.RequesterId == requesterId)
            .OrderByDescending(x => x.RequestedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<BreakGlassRequest>> ListAllAsync(CancellationToken ct = default) =>
        await db.BreakGlassRequests
            .AsNoTracking()
            .OrderByDescending(x => x.RequestedAt)
            .ToListAsync(ct);

    public async Task AddAsync(BreakGlassRequest request, CancellationToken ct = default)
    {
        await db.BreakGlassRequests.AddAsync(request, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(BreakGlassRequest request, CancellationToken ct = default)
    {
        db.BreakGlassRequests.Update(request);
        await db.SaveChangesAsync(ct);
    }
}
