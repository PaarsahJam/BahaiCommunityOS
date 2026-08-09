using CommunityOS.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CommunityOS.Identity.Infrastructure;

/// <summary>
/// Enables the EF Core tools to create and apply migrations without
/// booting the full web host (which requires a reachable database).
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=communityos_identity;Username=communityos;Password=communityos",
                npgsql => npgsql.MigrationsAssembly("CommunityOS.Identity.Infrastructure"))
            .Options;

        return new IdentityDbContext(options);
    }
}
