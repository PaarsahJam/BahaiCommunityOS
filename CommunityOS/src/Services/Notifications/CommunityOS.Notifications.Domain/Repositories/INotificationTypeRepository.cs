using CommunityOS.Notifications.Domain.Aggregates;

namespace CommunityOS.Notifications.Domain.Repositories;

public interface INotificationTypeRepository
{
    Task<NotificationType?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<List<NotificationType>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> AnyNotificationReferencesAsync(string code, CancellationToken cancellationToken = default);

    Task AddAsync(NotificationType notificationType, CancellationToken cancellationToken = default);

    Task UpdateAsync(NotificationType notificationType, CancellationToken cancellationToken = default);
}