using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CommunityOS.Correspondence.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for `dotnet ef` (established service pattern). Reads
/// the connection string from CORRESPONDENCE_DB_CONNECTION_STRING when present
/// so migrations can be generated against a reachable database.
/// </summary>
public sealed class CorrespondenceDbContextFactory : IDesignTimeDbContextFactory<CorrespondenceDbContext>
{
    public CorrespondenceDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CORRESPONDENCE_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5434;Database=communityos_correspondence;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<CorrespondenceDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "correspondence");
                npgsql.EnableRetryOnFailure(3);
            });
        return new CorrespondenceDbContext(optionsBuilder.Options);
    }
}
