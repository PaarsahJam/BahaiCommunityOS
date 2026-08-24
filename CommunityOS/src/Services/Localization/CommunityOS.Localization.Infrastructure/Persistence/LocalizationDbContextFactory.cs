using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CommunityOS.Localization.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for `dotnet ef` (established service pattern). Reads
/// the connection string from LOCALIZATION_DB_CONNECTION_STRING when present
/// so migrations can be generated against a reachable database.
/// </summary>
public sealed class LocalizationDbContextFactory : IDesignTimeDbContextFactory<LocalizationDbContext>
{
    public LocalizationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("LOCALIZATION_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=communityos_localization;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<LocalizationDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "localization");
                npgsql.EnableRetryOnFailure(3);
            });
        return new LocalizationDbContext(optionsBuilder.Options);
    }
}
