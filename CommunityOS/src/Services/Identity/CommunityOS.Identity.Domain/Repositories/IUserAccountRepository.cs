using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.ValueObjects;

namespace CommunityOS.Identity.Domain.Repositories;

public interface IUserAccountRepository
{
    Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// ADR-036 D3: loads the account row under a PostgreSQL row-level
    /// <c>FOR UPDATE</c> lock inside the caller's explicit transaction. The
    /// returned account reflects the state committed before the lock was
    /// acquired; its <c>SessionRevocationEpoch</c> is the ONLY authoritative
    /// value for a refresh/emergency decision. Callers must not load the
    /// account before acquiring the lock (EF would return the stale tracked
    /// instance) and no such pre-lock load happens in the refresh or emergency
    /// paths.
    /// </summary>
    Task<UserAccount?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);

    Task<UserAccount?> GetByEmailAsync(Email email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default);
    Task AddAsync(UserAccount userAccount, CancellationToken ct = default);
    Task UpdateAsync(UserAccount userAccount, CancellationToken ct = default);
}
