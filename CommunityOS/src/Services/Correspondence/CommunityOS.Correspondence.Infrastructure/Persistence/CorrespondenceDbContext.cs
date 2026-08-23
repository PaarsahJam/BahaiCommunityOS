using CommunityOS.Correspondence.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Correspondence.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Correspondence service (schema <c>correspondence</c>,
/// database <c>communityos_correspondence</c>). Born with the transactional
/// outbox: it models the MassTransit outbox entities so integration events
/// commit atomically with letter changes (ADR-015, ADR-028 decision 10).
/// </summary>
public sealed class CorrespondenceDbContext(DbContextOptions<CorrespondenceDbContext> options)
    : DbContext(options)
{
    public DbSet<Letter> Letters => Set<Letter>();

    public DbSet<Template> Templates => Set<Template>();

    public DbSet<LetterHold> LetterHolds => Set<LetterHold>();

    public DbSet<ExportActivity> ExportActivities => Set<ExportActivity>();

    public DbSet<OrganizationUnitReference> OrganizationUnitReferences => Set<OrganizationUnitReference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("correspondence");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CorrespondenceDbContext).Assembly);

        // Transactional outbox entities (MassTransit EF Core outbox). The bus
        // outbox stores forwarded integration events here; they are delivered
        // to RabbitMQ after the business transaction commits.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        base.OnModelCreating(modelBuilder);
    }
}
