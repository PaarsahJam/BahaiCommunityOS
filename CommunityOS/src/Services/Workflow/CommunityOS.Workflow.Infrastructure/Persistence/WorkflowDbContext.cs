using CommunityOS.Workflow.Domain.Aggregates;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Workflow.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Workflow service (schema <c>workflow</c>, database
/// <c>communityos_workflow</c>). Owns the outbox entities
/// (<c>OutboxMessage</c>/<c>OutboxState</c>/<c>InboxState</c>) so the
/// transactional outbox commits atomically with the task changes (ADR-015,
/// ratified at the Workflow outbox gate).
/// </summary>
public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options)
    : DbContext(options)
{
    public DbSet<WorkflowTask> WorkflowTasks => Set<WorkflowTask>();

    public DbSet<TaskDefinition> TaskDefinitions => Set<TaskDefinition>();

    public DbSet<OrganizationUnitReference> OrganizationUnitReferences => Set<OrganizationUnitReference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("workflow");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkflowDbContext).Assembly);

        // Transactional outbox entities (MassTransit EF Core outbox). The bus
        // outbox stores forwarded integration events here; they are delivered
        // to RabbitMQ after the business transaction commits.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        base.OnModelCreating(modelBuilder);
    }
}