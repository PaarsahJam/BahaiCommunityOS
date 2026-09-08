using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.ValueObjects;

namespace CommunityOS.Identity.Domain.Repositories;

public interface IUserAccountRepository
{
    Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserAccount?> GetByEmailAsync(Email email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default);
    Task AddAsync(UserAccount userAccount, CancellationToken ct = default);
    Task UpdateAsync(UserAccount userAccount, CancellationToken ct = default);
}
