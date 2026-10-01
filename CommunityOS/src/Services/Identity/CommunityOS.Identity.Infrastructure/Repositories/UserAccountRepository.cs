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

    public async Task<UserAccount?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) =>
        // C1 (ADR-036 Section 19, OD-18): the "xmin" system column is listed
        // explicitly. A bare SELECT * does not return PostgreSQL system columns,
        // so the rowversion concurrency value would otherwise never be loaded and
        // the resulting UPDATE predicate could not match.
        await db.UserAccounts.FromSqlInterpolated(
                $"""SELECT u.*, u.xmin FROM identity.user_accounts AS u WHERE u."Id" = {id} FOR UPDATE""")
            .SingleOrDefaultAsync(ct);

    public async Task<UserAccount?> GetByEmailAsync(Email email, CancellationToken ct = default) =>
        await db.UserAccounts.FirstOrDefaultAsync(x => x.Email == email, ct);

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
