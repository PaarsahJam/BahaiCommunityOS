using CommunityOS.Community.Domain.Aggregates;

namespace CommunityOS.Community.Domain.Repositories;

public interface IMeetingRepository
{
    Task<Meeting?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Meeting>> ListAsync(
        DateTime? from = null,
        DateTime? through = null,
        Guid? organizationUnitId = null,
        CancellationToken ct = default);
    Task AddAsync(Meeting meeting, CancellationToken ct = default);
    Task UpdateAsync(Meeting meeting, CancellationToken ct = default);
}
