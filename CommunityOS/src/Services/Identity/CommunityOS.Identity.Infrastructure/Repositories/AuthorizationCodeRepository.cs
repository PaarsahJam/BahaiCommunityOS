using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Identity.Infrastructure.Repositories;

public sealed class AuthorizationCodeRepository(IdentityDbContext db) : IAuthorizationCodeRepository
{
    public async Task<AuthorizationCode?> GetByCodeHashAsync(string codeHash, CancellationToken ct = default) =>
        await db.AuthorizationCodes.FirstOrDefaultAsync(x => x.CodeHash == codeHash, ct);

    public async Task AddAsync(AuthorizationCode authorizationCode, CancellationToken ct = default)
    {
        await db.AuthorizationCodes.AddAsync(authorizationCode, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AuthorizationCode authorizationCode, CancellationToken ct = default)
    {
        db.AuthorizationCodes.Update(authorizationCode);
        await db.SaveChangesAsync(ct);
    }
}
