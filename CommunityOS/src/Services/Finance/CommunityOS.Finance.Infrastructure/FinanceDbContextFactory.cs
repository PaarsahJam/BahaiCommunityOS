using CommunityOS.Finance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CommunityOS.Finance.Infrastructure;

/// <summary>
/// Enables the EF Core tools to create and apply migrations without booting
/// the full web host (which requires a reachable database).
/// </summary>
public sealed class FinanceDbContextFactory : IDesignTimeDbContextFactory<FinanceDbContext>
{
    public FinanceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=communityos_finance;Username=communityos;Password=communityos",
                npgsql => npgsql.MigrationsAssembly("CommunityOS.Finance.Infrastructure"))
            .Options;

        return new FinanceDbContext(options);
    }
}