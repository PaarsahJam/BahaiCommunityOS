using CommunityOS.Search.Application;
using CommunityOS.Search.Infrastructure;
using CommunityOS.Search.Infrastructure.Integration;
using CommunityOS.Search.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace CommunityOS.Search.IntegrationTests.Persistence;

/// <summary>
/// Verifies the EF Core mapping and the InitialCreate migration against a real
/// PostgreSQL instance (Testcontainers). Configuration errors (snake_case,
/// the tsvector column, the GIN index, the (source_type, source_id)
/// idempotency unique index, owned scope child table, and the MassTransit
/// inbox/outbox tables) surface here rather than in production. Also exercises
/// the projection writer's stale-event guard and index-log bookkeeping, the
/// full-text query, and the reconciler end-to-end.
///
/// NOTE: Docker is unavailable in the current environment, so this suite is
/// compile-only. It is executed in CI where a container runtime exists.
/// </summary>
public sealed class SearchPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("communityos_search")
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

    private SearchDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SearchDbContext>()
            .UseNpgsql(_connectionString!, npgsql => npgsql
                .MigrationsAssembly(typeof(SearchDbContext).Assembly.FullName))
            .Options;
        return new SearchDbContext(options);
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
    public async Task Migrations_create_all_search_tables_in_search_schema()
    {
        var tables = await QueryStringsAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'search' ORDER BY tablename;");

        tables.Should().Contain(
        [
            "search_documents", "search_document_scopes", "search_index_log",
            "organization_unit_references", "InboxState", "OutboxMessage", "OutboxState"
        ]);
    }

    [Fact]
    public async Task Search_vector_has_a_gin_index_and_idempotency_unique_index_exists()
    {
        var indexes = await QueryStringsAsync(
            """
            SELECT i.indexdef FROM pg_indexes i
            WHERE i.schemaname = 'search' AND i.tablename = 'search_documents';
            """);

        indexes.Should().Contain(def => def.Contains("USING gin", StringComparison.OrdinalIgnoreCase)
            && def.Contains("search_vector", StringComparison.OrdinalIgnoreCase));
        indexes.Should().Contain(def => def.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            && def.Contains("source_type", StringComparison.OrdinalIgnoreCase)
            && def.Contains("source_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Writer_upsert_is_queryable_by_full_text_and_updates_in_place()
    {
        await using var db = CreateContext();
        var writer = new SearchProjectionWriter(db);
        var unit = Guid.NewGuid();
        var sourceId = Guid.NewGuid();

        await writer.UpsertAsync("record", sourceId, "birth-registration", "birth-registration",
            "Active", false, unit, true, DateTime.UtcNow, true, CancellationToken.None);

        // A second applied event updates the same row (idempotent by source key).
        await writer.UpsertAsync("record", sourceId, null, null, "Archived", null, null, false,
            DateTime.UtcNow.AddMinutes(1), false, CancellationToken.None);

        db.ChangeTracker.Clear();
        var doc = await db.SearchDocuments.SingleAsync(d => d.SourceType == "record" && d.SourceId == sourceId);
        doc.Status.Should().Be("Archived");
        doc.OrganizationUnitId.Should().Be(unit);

        var repository = new SearchProjectionRepository(db, Microsoft.Extensions.Options.Options.Create(new SearchOptions()));
        var page = await repository.FetchCandidatesAsync(
            new SearchFilters("birth", ["record"], null, null, false), 0, 10, CancellationToken.None);
        page.Should().ContainSingle(r => r.SourceId == sourceId && r.Status == "Archived");
    }

    [Fact]
    public async Task Stale_events_are_rejected_and_do_not_touch_the_index_log()
    {
        await using var db = CreateContext();
        var writer = new SearchProjectionWriter(db);
        var sourceId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await writer.UpsertAsync("document", sourceId, "Policy", "policy", "Active", false, null, true, now, true, CancellationToken.None);
        var rejected = await writer.UpsertAsync("document", sourceId, null, null, "Deactivated", null, null, false, now.AddSeconds(-5), false, CancellationToken.None);
        rejected.Should().BeFalse();
        db.ChangeTracker.Clear();
        (await db.SearchDocuments.SingleAsync(d => d.SourceType == "document" && d.SourceId == sourceId)).Status
            .Should().Be("Active");

        var log = await db.SearchIndexLogs.SingleAsync(x => x.SourceType == "document");
        log.IndexedCount.Should().Be(1);
    }

    [Fact]
    public async Task Index_log_moves_forward_and_counts_applied_events()
    {
        await using var db = CreateContext();
        var writer = new SearchProjectionWriter(db);
        var id = Guid.NewGuid();
        var first = new DateTime(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
        var second = first.AddHours(1);

        await writer.UpsertAsync("workflow-task", id, "review", "review", "Created", false, Guid.NewGuid(), true, first, true, CancellationToken.None);
        await writer.UpsertAsync("workflow-task", Guid.NewGuid(), "review", "review", "Created", false, Guid.NewGuid(), true, second, true, CancellationToken.None);
        await writer.UpsertAsync("workflow-task", id, null, null, "Completed", false, null, false, first.AddMinutes(-1), false, CancellationToken.None);

        db.ChangeTracker.Clear();
        var log = await db.SearchIndexLogs.SingleAsync(x => x.SourceType == "workflow-task");
        log.IndexedCount.Should().Be(2);
        log.LastEventOccurredOn.Should().Be(second);
    }

    [Fact]
    public async Task Reconciler_recomputes_vectors_and_syncs_log_counts()
    {
        await using var db = CreateContext();
        var writer = new SearchProjectionWriter(db);
        var id = Guid.NewGuid();

        await writer.UpsertAsync("knowledge-question", id, "Question", "question", "Published", false, null, true, DateTime.UtcNow, true, CancellationToken.None);

        // Simulate drift: wipe the vector and desync the count.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE search.search_documents SET search_vector = '' WHERE source_id = {id}",
            CancellationToken.None);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE search.search_index_log SET indexed_count = 99 WHERE source_type = 'knowledge-question';",
            CancellationToken.None);

        var reconciler = new SearchProjectionReconciler(db);
        await reconciler.ReconcileAsync(null, CancellationToken.None);

        db.ChangeTracker.Clear();
        var repository = new SearchProjectionRepository(db, Microsoft.Extensions.Options.Options.Create(new SearchOptions()));
        var page = await repository.FetchCandidatesAsync(
            new SearchFilters("question", ["knowledge-question"], null, null, false), 0, 10, CancellationToken.None);
        page.Should().ContainSingle(r => r.SourceId == id);

        var log = await db.SearchIndexLogs.SingleAsync(x => x.SourceType == "knowledge-question");
        log.IndexedCount.Should().Be(1);
    }
}
