using CommunityOS.Records.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CommunityOS.Records.Infrastructure;

/// <summary>
/// Enables the EF Core tools to create and apply migrations without booting
/// the full web host (which requires a reachable database).
/// </summary>
public sealed class RecordsDbContextFactory : IDesignTimeDbContextFactory<RecordsDbContext>
{
    public RecordsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RecordsDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=communityos_records;Username=communityos;Password=communityos",
                npgsql => npgsql.MigrationsAssembly("CommunityOS.Records.Infrastructure"))
            .Options;

        return new RecordsDbContext(options);
    }
}