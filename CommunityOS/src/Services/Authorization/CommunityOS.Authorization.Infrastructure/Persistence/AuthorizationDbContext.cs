using CommunityOS.Authorization.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Authorization.Infrastructure.Persistence;

public sealed class AuthorizationDbContext(DbContextOptions<AuthorizationDbContext> options)
    : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();
    public DbSet<AuthorizationRelationship> Relationships => Set<AuthorizationRelationship>();
    public DbSet<Delegation> Delegations => Set<Delegation>();
    public DbSet<BreakGlassRequest> BreakGlassRequests => Set<BreakGlassRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("authorization");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuthorizationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
