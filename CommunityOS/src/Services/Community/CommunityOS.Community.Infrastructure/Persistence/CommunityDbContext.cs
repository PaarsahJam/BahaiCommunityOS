using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Community.Infrastructure.Persistence;

public sealed class CommunityDbContext(DbContextOptions<CommunityDbContext> options)
    : DbContext(options)
{
    public DbSet<Community> Communities => Set<Community>();
    public DbSet<LocalUnit> LocalUnits  => Set<LocalUnit>();
    public DbSet<Cluster>   Clusters    => Set<Cluster>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("community");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommunityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
