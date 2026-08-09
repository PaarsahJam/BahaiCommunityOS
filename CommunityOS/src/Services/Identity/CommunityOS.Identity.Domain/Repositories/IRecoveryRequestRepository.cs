using CommunityOS.Identity.Domain.Entities;

namespace CommunityOS.Identity.Domain.Repositories;

public interface IRecoveryRequestRepository
{
    Task<RecoveryRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<RecoveryRequest?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<RecoveryRequest>> GetRecentByUserAsync(
        Guid userAccountId, int take = 20, CancellationToken ct = default);
    Task AddAsync(RecoveryRequest request, CancellationToken ct = default);
    Task UpdateAsync(RecoveryRequest request, CancellationToken ct = default);
}
