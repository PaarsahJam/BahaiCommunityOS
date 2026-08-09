using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Identity.Infrastructure.Repositories;

public sealed class SecurityEventRepository(IdentityDbContext db) : ISecurityEventRepository
{
    public async Task AddAsync(SecurityEvent securityEvent, CancellationToken ct = default)
    {
        await db.SecurityEvents.AddAsync(securityEvent, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SecurityEvent>> GetByUserAsync(
        Guid userAccountId, int take = 100, CancellationToken ct = default) =>
        await db.SecurityEvents
            .Where(x => x.UserAccountId == userAccountId)
            .OrderByDescending(x => x.OccurredOn)
            .Take(take)
            .ToListAsync(ct);
}
