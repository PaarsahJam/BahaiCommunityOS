using CommunityOS.Identity.Domain.Aggregates;

namespace CommunityOS.Identity.Domain.Repositories;

public interface ISessionRepository
{
    Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Session?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<Session>> GetActiveByUserAsync(Guid userAccountId, CancellationToken ct = default);
    Task<IReadOnlyList<Session>> GetByFamilyAsync(Guid tokenFamilyId, CancellationToken ct = default);
    Task AddAsync(Session session, CancellationToken ct = default);
    Task UpdateAsync(Session session, CancellationToken ct = default);
    Task RevokeAllForUserAsync(Guid userAccountId, string reason, CancellationToken ct = default);
    Task RevokeAllExceptFamilyForUserAsync(
        Guid userAccountId, Guid tokenFamilyId, string reason, CancellationToken ct = default);
}
