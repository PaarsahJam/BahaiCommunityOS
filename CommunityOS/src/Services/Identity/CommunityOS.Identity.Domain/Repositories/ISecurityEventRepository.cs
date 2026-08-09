using CommunityOS.Identity.Domain.Entities;

namespace CommunityOS.Identity.Domain.Repositories;

public interface ISecurityEventRepository
{
    Task AddAsync(SecurityEvent securityEvent, CancellationToken ct = default);
    Task<IReadOnlyList<SecurityEvent>> GetByUserAsync(
        Guid userAccountId, int take = 100, CancellationToken ct = default);
}
