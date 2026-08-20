using CommunityOS.Notifications.Domain.Aggregates;

namespace CommunityOS.Notifications.Domain.Repositories;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Notification?> GetBySourceAsync(
        string typeCode, string sourceType, Guid sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inbox candidates for a member: notifications where the member is a
    /// recipient. Sensitive rows are filtered by the caller after scope/flag
    /// evaluation (relationship tuple interpreted via recipient rows, ADR-025).
    /// </summary>
    Task<List<Notification>> ListInboxCandidatesAsync(
        Guid memberId, int limit, int offset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Not-yet-dispatched notifications eligible for the dispatch worker
    /// (immediate <c>ScheduledFor</c> is null, or due at/before
    /// <paramref name="now"/>).
    /// </summary>
    Task<List<Notification>> ListQueuedForDispatchAsync(
        int limit, DateTime now, CancellationToken cancellationToken = default);

    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts the notification. When a concurrent create already opened a
    /// notification for the same (type, source type, source id, channel), the
    /// unique filtered index rejects the insert and the existing notification is
    /// returned (idempotent create under concurrency).
    /// </summary>
    Task<Notification> AddIfAbsentAsync(Notification notification, CancellationToken cancellationToken = default);

    Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default);
}