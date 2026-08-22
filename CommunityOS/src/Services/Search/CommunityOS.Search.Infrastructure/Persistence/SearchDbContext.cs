using CommunityOS.Search.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Search.Infrastructure.Persistence;

public sealed class SearchDbContext(DbContextOptions<SearchDbContext> options) : DbContext(options)
{
    public DbSet<SearchDocument> SearchDocuments => Set<SearchDocument>();
    public DbSet<OrganizationUnitReference> OrganizationUnitReferences => Set<OrganizationUnitReference>();
    public DbSet<SearchIndexLog> SearchIndexLogs => Set<SearchIndexLog>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("search");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SearchDbContext).Assembly);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        base.OnModelCreating(modelBuilder);
    }
}
