using CommunityOS.Authorization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CommunityOS.Authorization.Infrastructure;

/// <summary>
/// Enables the EF Core tools to create and apply migrations without booting
/// the full web host (which requires a reachable database).
/// </summary>
public sealed class AuthorizationDbContextFactory : IDesignTimeDbContextFactory<AuthorizationDbContext>
{
    public AuthorizationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuthorizationDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=communityos_authorization;Username=communityos;Password=communityos",
                npgsql => npgsql.MigrationsAssembly("CommunityOS.Authorization.Infrastructure"))
            .Options;

        return new AuthorizationDbContext(options);
    }
}
