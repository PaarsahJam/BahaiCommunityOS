using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CommunityOS.Audit.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for `dotnet ef` (mirrors the Search service factory).
/// Reads the connection string from AUDIT_DB_CONNECTION_STRING when present so
/// migrations can be generated against a reachable database.
/// </summary>
public sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AUDIT_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5434;Database=communityos_audit;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "audit");
                npgsql.EnableRetryOnFailure(3);
            });
        return new AuditDbContext(optionsBuilder.Options);
    }
}
