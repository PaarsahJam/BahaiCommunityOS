using CommunityOS.Identity.Domain.Aggregates;

namespace CommunityOS.Identity.Domain.Repositories;

public interface IAuthorizationCodeRepository
{
    Task<AuthorizationCode?> GetByCodeHashAsync(string codeHash, CancellationToken ct = default);
    Task AddAsync(AuthorizationCode authorizationCode, CancellationToken ct = default);
    Task UpdateAsync(AuthorizationCode authorizationCode, CancellationToken ct = default);
}
