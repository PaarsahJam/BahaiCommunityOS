using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Identity.Infrastructure.Repositories;

public sealed class OAuthClientRepository(IdentityDbContext db) : IOAuthClientRepository
{
    public async Task<OAuthClient?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.OAuthClients.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<OAuthClient?> GetByClientIdAsync(string clientId, CancellationToken ct = default) =>
        await db.OAuthClients.FirstOrDefaultAsync(x => x.ClientId == clientId, ct);

    public async Task AddAsync(OAuthClient client, CancellationToken ct = default)
    {
        await db.OAuthClients.AddAsync(client, ct);
        await db.SaveChangesAsync(ct);
    }
}
