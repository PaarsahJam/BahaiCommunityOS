using CommunityOS.Documents.Domain.Aggregates;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Documents.Infrastructure.Persistence;

public sealed class DocumentsDbContext(DbContextOptions<DocumentsDbContext> options)
    : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();

    public DbSet<OrganizationUnitReference> OrganizationUnitReferences => Set<OrganizationUnitReference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("documents");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentsDbContext).Assembly);

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        base.OnModelCreating(modelBuilder);
    }
}