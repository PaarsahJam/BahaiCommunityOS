using CommunityOS.Notifications.Domain.Aggregates;

namespace CommunityOS.Notifications.Domain.Repositories;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetPendingAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetByMemberAsync(Guid memberId, CancellationToken ct = default);
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task UpdateAsync(Notification notification, CancellationToken ct = default);
}
