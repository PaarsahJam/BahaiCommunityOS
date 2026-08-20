using CommunityOS.Notifications.Domain.Aggregates;
using CommunityOS.Notifications.Domain.Entities;
using CommunityOS.Notifications.Domain.Enumerations;
using CommunityOS.Notifications.Domain.Repositories;
using CommunityOS.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Notifications.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="INotificationRepository"/>. Owned
/// collections are included explicitly so the aggregate graph is fully
/// materialized for guard checks and mutations.
/// </summary>
public sealed class NotificationRepository(NotificationsDbContext db) : INotificationRepository
{
    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public Task<Notification?> GetBySourceAsync(
        string typeCode, string sourceType, Guid sourceId, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(n =>
            n.TypeCode == typeCode &&
            n.SourceType == sourceType &&
            n.SourceId == sourceId, cancellationToken);

    public Task<List<Notification>> ListInboxCandidatesAsync(
        Guid memberId, int limit, int offset, CancellationToken cancellationToken = default) =>
        Query()
            .Where(n => n.Recipients.Any(r => r.MemberId == memberId))
            .OrderByDescending(n => n.CreatedOn)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<List<Notification>> ListQueuedForDispatchAsync(
        int limit, DateTime now, CancellationToken cancellationToken = default) =>
        Query()
            .Where(n => n.Status == NotificationLifecycleStatus.Queued &&
                (n.ScheduledFor == null || n.ScheduledFor <= now))
            .OrderBy(n => n.CreatedOn)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        db.Notifications.Add(notification);
        return db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Notification> AddIfAbsentAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        db.Notifications.Add(notification);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return notification;
        }
        catch (DbUpdateException)
        {
            // A concurrent create already opened a notification for the same
            // (type, source type, source id, channel); the unique filtered index
            // rejected our insert. Return the existing notification so creation
            // stays idempotent under concurrency.
            if (notification.SourceType is not null && notification.SourceId is { } sourceId)
            {
                db.Entry(notification).State = EntityState.Detached;
                var existing = await GetBySourceAsync(
                    notification.TypeCode, notification.SourceType, sourceId, cancellationToken);
                if (existing is not null)
                    return existing;
            }

            throw;
        }
    }

    public async Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        db.Notifications.Update(notification);
        await db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Notification> Query() =>
        db.Notifications
            .Include(n => n.Recipients)
            .Include(n => n.AdditionalScopes);
}

public sealed class NotificationTypeRepository(NotificationsDbContext db) : INotificationTypeRepository
{
    public Task<NotificationType?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        db.NotificationTypes.FirstOrDefaultAsync(t => t.Code == code, cancellationToken);

    public Task<List<NotificationType>> ListAsync(CancellationToken cancellationToken = default) =>
        db.NotificationTypes.OrderBy(t => t.Code).ToListAsync(cancellationToken);

    public Task<bool> AnyNotificationReferencesAsync(string code, CancellationToken cancellationToken = default) =>
        db.Notifications.AnyAsync(n => n.TypeCode == code, cancellationToken);

    public async Task AddAsync(NotificationType notificationType, CancellationToken cancellationToken = default)
    {
        db.NotificationTypes.Add(notificationType);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(NotificationType notificationType, CancellationToken cancellationToken = default)
    {
        db.NotificationTypes.Update(notificationType);
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class NotificationPreferenceRepository(NotificationsDbContext db)
    : INotificationPreferenceRepository
{
    public Task<NotificationPreference?> GetAsync(
        Guid memberId, string typeCode, int channelId, CancellationToken cancellationToken = default) =>
        db.NotificationPreferences.FirstOrDefaultAsync(p =>
            p.MemberId == memberId &&
            p.TypeCode == typeCode &&
            p.Channel.Id == channelId, cancellationToken);

    public Task<List<NotificationPreference>> ListByMemberAsync(
        Guid memberId, CancellationToken cancellationToken = default) =>
        db.NotificationPreferences
            .Where(p => p.MemberId == memberId)
            .ToListAsync(cancellationToken);

    public Task<List<NotificationPreference>> ListDisabledAsync(
        string typeCode, IReadOnlyList<Guid> memberIds, CancellationToken cancellationToken = default) =>
        db.NotificationPreferences
            .Where(p => p.TypeCode == typeCode &&
                p.Enabled == false &&
                memberIds.Contains(p.MemberId))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(NotificationPreference preference, CancellationToken cancellationToken = default)
    {
        db.NotificationPreferences.Add(preference);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(NotificationPreference preference, CancellationToken cancellationToken = default)
    {
        db.NotificationPreferences.Update(preference);
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class NotificationsOrganizationUnitReferenceRepository(NotificationsDbContext db)
    : IOrganizationUnitReferenceRepository
{
    public Task<OrganizationUnitReference?> GetByOrganizationUnitIdAsync(
        Guid organizationUnitId, CancellationToken cancellationToken = default) =>
        db.OrganizationUnitReferences
            .FirstOrDefaultAsync(r => r.OrganizationUnitId == organizationUnitId, cancellationToken);

    public Task<List<OrganizationUnitReference>> ListAsync(CancellationToken cancellationToken = default) =>
        db.OrganizationUnitReferences.ToListAsync(cancellationToken);

    public async Task AddAsync(OrganizationUnitReference reference, CancellationToken cancellationToken = default)
    {
        db.OrganizationUnitReferences.Add(reference);
        await db.SaveChangesAsync(cancellationToken);
    }
}