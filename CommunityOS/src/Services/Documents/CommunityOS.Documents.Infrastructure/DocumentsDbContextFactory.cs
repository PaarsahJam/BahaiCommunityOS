using CommunityOS.Documents.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CommunityOS.Documents.Infrastructure;

/// <summary>
/// Enables the EF Core tools to create and apply migrations without booting
/// the full web host (which requires a reachable database).
/// </summary>
public sealed class DocumentsDbContextFactory : IDesignTimeDbContextFactory<DocumentsDbContext>
{
    public DocumentsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=communityos_documents;Username=communityos;Password=communityos",
                npgsql => npgsql.MigrationsAssembly("CommunityOS.Documents.Infrastructure"))
            .Options;

        return new DocumentsDbContext(options);
    }
}