using CommunityOS.Community.Domain.Aggregates;

namespace CommunityOS.Community.Domain.Repositories;

public interface IPersonRepository
{
    Task<Person?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Person?> GetByIdentityAccountIdAsync(Guid identityAccountId, CancellationToken ct = default);
    Task<IReadOnlyList<Person>> ListAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Person person, CancellationToken ct = default);
    Task UpdateAsync(Person person, CancellationToken ct = default);
}
