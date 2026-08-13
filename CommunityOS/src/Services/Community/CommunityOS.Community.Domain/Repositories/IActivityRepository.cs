using CommunityOS.Community.Domain.Aggregates;

namespace CommunityOS.Community.Domain.Repositories;

public interface IActivityRepository
{
    Task<Activity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Activity>> ListAsync(
        DateTime? from = null,
        DateTime? through = null,
        Guid? organizationUnitId = null,
        CancellationToken ct = default);
    Task AddAsync(Activity activity, CancellationToken ct = default);
    Task UpdateAsync(Activity activity, CancellationToken ct = default);
}
