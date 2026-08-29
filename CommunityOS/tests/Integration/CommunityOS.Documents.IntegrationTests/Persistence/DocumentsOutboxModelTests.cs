using CommunityOS.Documents.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Documents.IntegrationTests.Persistence;

/// <summary>
/// Asserts the MassTransit transactional outbox contract in the Documents EF
/// Core model, built offline from the configuration assemblies (ADR-015 at the
/// Documents outbox gate). No Docker or live database is required: the
/// relational model is produced solely from
/// <see cref="DocumentsDbContext.OnModelCreating"/>.
/// </summary>
public sealed class DocumentsOutboxModelTests
{
    private static DocumentsDbContext CreateOfflineContext()
    {
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=communityos_documents;Username=communityos;Password=communityos",
                npgsql => npgsql.MigrationsAssembly("CommunityOS.Documents.Infrastructure"))
            .Options;

        return new DocumentsDbContext(options);
    }

    [Fact]
    public void Context_uses_the_documents_schema_and_owns_the_mass_transit_outbox()
    {
        using var db = CreateOfflineContext();

        db.Model.GetRelationalModel();

        db.Model.GetDefaultSchema().Should().Be("documents");

        var outbox = db.Model.GetEntityTypes()
            .Where(e => e.ClrType.FullName?.StartsWith("MassTransit.", StringComparison.Ordinal) == true)
            .ToList();
        outbox.Should().NotBeEmpty();
        outbox.Should().OnlyContain(e => e.GetSchema() == "documents");
        outbox.Select(e => e.GetTableName()).Should().Contain(
        [
            "InboxState",
            "OutboxState",
            "OutboxMessage"
        ]);
    }
}