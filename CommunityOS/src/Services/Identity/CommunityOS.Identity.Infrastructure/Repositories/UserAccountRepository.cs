using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;
using CommunityOS.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Identity.Infrastructure.Repositories;

public sealed class UserAccountRepository(IdentityDbContext db) : IUserAccountRepository
{
    public async Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.UserAccounts.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<UserAccount?> GetByEmailAsync(Email email, CancellationToken ct = default) =>
        await db.UserAccounts.FirstOrDefaultAsync(x => x.Email == email, ct);

    public async Task<UserAccount?> GetByMfaMethodIdAsync(Guid mfaMethodId, CancellationToken ct = default) =>
        await db.UserAccounts
            .FirstOrDefaultAsync(x => x.MfaMethods.Any(m => m.Id == mfaMethodId), ct);

    public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default) =>
        await db.UserAccounts.AnyAsync(x => x.Email == email, ct);

    public async Task AddAsync(UserAccount userAccount, CancellationToken ct = default)
    {
        await db.UserAccounts.AddAsync(userAccount, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(UserAccount userAccount, CancellationToken ct = default)
    {
        db.UserAccounts.Update(userAccount);
        await db.SaveChangesAsync(ct);
    }
}
