using CommunityOS.Documents.Domain.Aggregates;
using CommunityOS.Documents.Domain.Enumerations;
using CommunityOS.Documents.Infrastructure.Persistence;
using CommunityOS.Documents.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CommunityOS.Documents.IntegrationTests.Persistence;

/// <summary>
/// Verifies the EF Core mapping and the InitialCreateDocuments migration
/// against a real PostgreSQL instance (Testcontainers). Configuration errors
/// (snake_case, owned child tables, enums stored as ints, the documents
/// schema) surface here rather than in production.
/// </summary>
public sealed class DocumentsPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("communityos_documents")
        .WithUsername("communityos")
        .WithPassword("communityos")
        .Build();

    private string? _connectionString;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private DocumentsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseNpgsql(_connectionString!, npgsql => npgsql
                .MigrationsAssembly(typeof(DocumentsDbContext).Assembly.FullName))
            .Options;
        return new DocumentsDbContext(options);
    }

    private async Task<List<string>> QueryStringsAsync(string sql)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var results = new List<string>();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(reader.GetString(0));

        return results;
    }

    [Fact]
    public async Task Migrations_create_all_documents_tables_in_documents_schema()
    {
        var tables = await QueryStringsAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'documents' ORDER BY tablename;");

        tables.Should().Contain(
        [
            "documents",
            "organization_unit_references",
            "document_versions",
            "document_scopes",
            "document_references",
            "document_classification"
        ]);
    }

    [Fact]
    public async Task Migrations_can_be_applied_idempotently()
    {
        await using var db = CreateContext();

        await db.Database.MigrateAsync();

        var tables = await QueryStringsAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'documents';");
        tables.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Document_repository_roundtrip_preserves_versions_scopes_and_references()
    {
        await using var db = CreateContext();
        var repo = new DocumentRepository(db);

        var actor = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var document = Document.Create("Feast Agenda", "Draft agenda", Guid.NewGuid(), "person", actor, actor, now);
        document.AddVersion(
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
            "text/plain",
            7,
            "agenda.txt",
            DocumentSources.Member,
            actor,
            now,
            ScanStatus.NotScanned);
        document.AddOrganizationScope(Guid.NewGuid(), actor, now);
        document.AddReference("passage", Guid.NewGuid(), "passage-1", actor, now);
        await repo.AddAsync(document);

        var fetched = await repo.GetByIdAsync(document.Id);
        fetched.Should().NotBeNull();
        fetched!.Title.Should().Be("Feast Agenda");
        fetched.Status.Should().Be(DocumentStatus.Active);
        fetched.CurrentVersion.Should().NotBeNull();
        fetched.CurrentVersion!.VersionNumber.Should().Be(1);
        fetched.CurrentVersion.ContentHash.Should().Be(document.CurrentVersion!.ContentHash);
        fetched.Scopes.Should().ContainSingle();
        fetched.References.Should().ContainSingle(r => r.SourceContext == "passage");

        // ListByOrganizationUnit surfaces both the primary unit and scope units.
        var byPrimary = await repo.ListByOrganizationUnitAsync(document.OrganizationUnitId!.Value);
        byPrimary.Should().ContainSingle(d => d.Id == document.Id);
        var byScope = await repo.ListByOrganizationUnitAsync(document.Scopes.Single().OrganizationUnitId);
        byScope.Should().ContainSingle(d => d.Id == document.Id);
    }

    [Fact]
    public async Task Document_repository_preserves_version_immutability_and_scan_state()
    {
        await using var db = CreateContext();
        var repo = new DocumentRepository(db);

        var actor = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var document = Document.Create("Feast Agenda", "Draft agenda", Guid.NewGuid(), "person", actor, actor, now);
        document.AddVersion(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "text/plain",
            4,
            "a.txt",
            DocumentSources.Member,
            actor,
            now,
            ScanStatus.NotScanned);
        await repo.AddAsync(document);

        var fetched = await repo.GetByIdAsync(document.Id);
        fetched!.UpdateScanStatus(fetched.CurrentVersion!.Id, ScanStatus.Clean, now);
        await repo.UpdateAsync(fetched);

        var updated = await repo.GetByIdAsync(document.Id);
        updated!.CurrentVersion!.ScanStatus.Should().Be(ScanStatus.Clean);
        updated.Versions.Should().ContainSingle();
        updated.Versions.Single().ContentHash.Should().Be(document.CurrentVersion!.ContentHash);
    }

    [Fact]
    public async Task OrganizationUnitReference_repository_upserts_read_model()
    {
        await using var db = CreateContext();
        var repo = new OrganizationUnitReferenceRepository(db);

        var unitId = Guid.NewGuid();
        var reference = OrganizationUnitReference.Create(unitId, Guid.NewGuid(), "Local Spiritual Assembly", "LSA", null, DateTime.UtcNow);
        await repo.UpsertUnitAsync(reference);

        var fetched = await repo.GetUnitByIdAsync(unitId);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("Local Spiritual Assembly");

        reference.Sync("LSA of Tehran", "LSA", Guid.NewGuid(), DateTime.UtcNow);
        await repo.UpsertUnitAsync(reference);

        var updated = await repo.GetUnitByIdAsync(unitId);
        updated!.Name.Should().Be("LSA of Tehran");
        updated.ParentId.Should().NotBeNull();
    }
}