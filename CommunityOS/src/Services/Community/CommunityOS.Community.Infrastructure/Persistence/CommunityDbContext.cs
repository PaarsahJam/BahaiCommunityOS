using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Entities;
using CommunityAggregate = CommunityOS.Community.Domain.Aggregates.Community;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Community.Infrastructure.Persistence;

public sealed class CommunityDbContext(DbContextOptions<CommunityDbContext> options)
    : DbContext(options)
{
    public DbSet<CommunityAggregate> Communities => Set<CommunityAggregate>();
    public DbSet<LocalUnit> LocalUnits  => Set<LocalUnit>();
    public DbSet<Cluster>   Clusters    => Set<Cluster>();
    public DbSet<OrganizationReference> OrganizationReferences => Set<OrganizationReference>();
    public DbSet<OrganizationUnitReference> OrganizationUnitReferences => Set<OrganizationUnitReference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("community");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommunityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
