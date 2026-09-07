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
    /// Recipient-scoped member inbox for the authenticated actor (ADR-027):
    /// notifications the member is a recipient of, newest-first, bounded by
    /// skip/take at the query boundary. Sensitive notifications are excluded at
    /// the query boundary — they are not part of the member read contract
    /// (fail-closed). The recipient identity is never client-supplied.
    /// </summary>
    Task<List<Notification>> ListMemberInboxAsync(
        Guid memberId, int limit, int offset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Count of delivered-but-unread notifications for the member (recipient
    /// status <c>Delivered</c>, not yet <c>Read</c>), excluding sensitive
    /// notifications. Recipient-filtered before aggregation; never loads a
    /// recipient distribution (ADR-027).
    /// </summary>
    Task<int> CountUnreadByMemberAsync(
        Guid memberId, CancellationToken cancellationToken = default);

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