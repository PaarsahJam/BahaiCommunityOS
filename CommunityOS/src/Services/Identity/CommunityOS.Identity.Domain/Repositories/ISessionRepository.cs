using CommunityOS.Identity.Domain.Aggregates;

namespace CommunityOS.Identity.Domain.Repositories;

public interface ISessionRepository
{
    Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Session?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<Session>> GetActiveByUserAsync(Guid userAccountId, CancellationToken ct = default);
    Task<IReadOnlyList<Session>> GetByFamilyAsync(Guid tokenFamilyId, CancellationToken ct = default);

    /// <summary>
    /// ADR-036 D3: re-reads <paramref name="session"/> from the database,
    /// refreshing its tracked state. Called after the account row is locked so
    /// the refresh decision observes reuse/revocation committed by the
    /// operation that won the lock; EF would otherwise keep returning the
    /// tracked pre-lock snapshot.
    /// </summary>
    Task ReloadAsync(Session session, CancellationToken ct = default);

    Task AddAsync(Session session, CancellationToken ct = default);
    Task UpdateAsync(Session session, CancellationToken ct = default);
    Task RevokeAllForUserAsync(Guid userAccountId, string reason, CancellationToken ct = default);
    Task RevokeAllExceptFamilyForUserAsync(
        Guid userAccountId, Guid tokenFamilyId, string reason, CancellationToken ct = default);
}
