using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Identity.Infrastructure.Repositories;

public sealed class SessionRepository(IdentityDbContext db) : ISessionRepository
{
    public async Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Sessions.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Session?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken ct = default) =>
        await db.Sessions.FirstOrDefaultAsync(x => x.RefreshTokenHash == refreshTokenHash, ct);

    public async Task<IReadOnlyList<Session>> GetActiveByUserAsync(Guid userAccountId, CancellationToken ct = default) =>
        await db.Sessions
            .Where(x => x.UserAccountId == userAccountId && !x.IsRevoked)
            .OrderByDescending(x => x.LastUsedOn)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Session>> GetByFamilyAsync(Guid tokenFamilyId, CancellationToken ct = default) =>
        await db.Sessions
            .Where(x => x.TokenFamilyId == tokenFamilyId)
            .ToListAsync(ct);

    public async Task AddAsync(Session session, CancellationToken ct = default)
    {
        await db.Sessions.AddAsync(session, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Session session, CancellationToken ct = default)
    {
        db.Sessions.Update(session);
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAllForUserAsync(Guid userAccountId, string reason, CancellationToken ct = default)
    {
        var active = await db.Sessions
            .Where(x => x.UserAccountId == userAccountId && !x.IsRevoked)
            .ToListAsync(ct);

        foreach (var session in active)
            session.Revoke(reason);

        if (active.Count != 0)
            await db.SaveChangesAsync(ct);
    }
}
