using CommunityOS.Identity.Domain.Aggregates;

namespace CommunityOS.Identity.Domain.Repositories;

public interface IOAuthClientRepository
{
    Task<OAuthClient?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<OAuthClient?> GetByClientIdAsync(string clientId, CancellationToken ct = default);
    Task AddAsync(OAuthClient client, CancellationToken ct = default);
}
