using CommunityOS.Records.Domain.Aggregates;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Records.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Records service (schema <c>records</c>, database
/// <c>communityos_records</c>). Owns the outbox entities
/// (<c>OutboxMessage</c>/<c>OutboxState</c>/<c>InboxState</c>) so the
/// transactional outbox commits atomically with the record changes (ADR-015,
/// ratified at the Records gate).
/// </summary>
public sealed class RecordsDbContext(DbContextOptions<RecordsDbContext> options)
    : DbContext(options)
{
    public DbSet<Record> Records => Set<Record>();

    public DbSet<RecordCategory> RecordCategories => Set<RecordCategory>();

    public DbSet<RetentionSchedule> RetentionSchedules => Set<RetentionSchedule>();

    public DbSet<OrganizationUnitReference> OrganizationUnitReferences => Set<OrganizationUnitReference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("records");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RecordsDbContext).Assembly);

        // Transactional outbox entities (MassTransit EF Core outbox). The bus
        // outbox stores forwarded integration events here; they are delivered
        // to RabbitMQ after the business transaction commits.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        base.OnModelCreating(modelBuilder);
    }
}