using CommunityOS.Notifications.Domain.Aggregates;
using CommunityOS.Notifications.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Notifications.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Notifications service (schema <c>notifications</c>,
/// database <c>communityos_notifications</c>). Owns the outbox entities
/// (<c>OutboxMessage</c>/<c>OutboxState</c>/<c>InboxState</c>) so the
/// transactional outbox commits atomically with the notification changes
/// (ADR-015, ratified at the Notifications integration gate).
/// </summary>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
    : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<NotificationType> NotificationTypes => Set<NotificationType>();

    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    public DbSet<OrganizationUnitReference> OrganizationUnitReferences => Set<OrganizationUnitReference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notifications");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);

        // Transactional outbox entities (MassTransit EF Core outbox). The bus
        // outbox stores forwarded integration events here; they are delivered
        // to RabbitMQ after the business transaction commits.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        base.OnModelCreating(modelBuilder);
    }
}