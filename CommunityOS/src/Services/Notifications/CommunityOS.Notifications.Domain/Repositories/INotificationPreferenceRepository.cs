using CommunityOS.Notifications.Domain.Entities;

namespace CommunityOS.Notifications.Domain.Repositories;

public interface INotificationPreferenceRepository
{
    Task<NotificationPreference?> GetAsync(
        Guid memberId, string typeCode, int channelId, CancellationToken cancellationToken = default);

    Task<List<NotificationPreference>> ListByMemberAsync(Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>Opt-out rows (member, type) for the dispatch worker's recipient filtering.</summary>
    Task<List<NotificationPreference>> ListDisabledAsync(
        string typeCode, IReadOnlyList<Guid> memberIds, CancellationToken cancellationToken = default);

    Task AddAsync(NotificationPreference preference, CancellationToken cancellationToken = default);

    Task UpdateAsync(NotificationPreference preference, CancellationToken cancellationToken = default);
}