using CommunityOS.Finance.Domain.Aggregates;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Finance.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Finance service (schema <c>finance</c>, database
/// <c>communityos_finance</c>). Owns the outbox entities
/// (<c>OutboxMessage</c>/<c>OutboxState</c>/<c>InboxState</c>) so the
/// transactional outbox commits atomically with the ledger changes (ADR-015,
/// ratified at the Finance gate).
/// </summary>
public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options)
    : DbContext(options)
{
    public DbSet<Fund> Funds => Set<Fund>();

    public DbSet<FinancialTransaction> Transactions => Set<FinancialTransaction>();

    public DbSet<OrganizationUnitReference> OrganizationUnitReferences => Set<OrganizationUnitReference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("finance");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceDbContext).Assembly);

        // Transactional outbox entities (MassTransit EF Core outbox). The bus
        // outbox stores forwarded integration events here; they are delivered
        // to RabbitMQ after the business transaction commits.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        base.OnModelCreating(modelBuilder);
    }
}