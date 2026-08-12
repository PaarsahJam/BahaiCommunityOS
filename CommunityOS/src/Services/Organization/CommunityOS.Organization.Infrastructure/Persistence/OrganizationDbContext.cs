using CommunityOS.Organization.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Organization.Infrastructure.Persistence;

using Organization = CommunityOS.Organization.Domain.Aggregates.Organization;

public sealed class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options)
    : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationUnit> OrganizationUnits => Set<OrganizationUnit>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Committee> Committees => Set<Committee>();
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<DelegationFact> DelegationFacts => Set<DelegationFact>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("organization");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}