using CommunityOS.Audit.Application;
using CommunityOS.Audit.Domain;
using CommunityOS.Audit.Infrastructure;
using CommunityOS.Audit.Infrastructure.Persistence;
using CommunityOS.Audit.Infrastructure.Retention;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace CommunityOS.Audit.IntegrationTests.Persistence;

/// <summary>
/// Verifies the EF Core mapping and the InitialCreateAudit migration against a
/// real PostgreSQL instance (Testcontainers): schema layout, the unique
/// source-event hash index, and — critically — the database-level immutability
/// triggers plus the guarded purge path (ADR-027 decisions 2 and 14).
///
/// NOTE: Docker is unavailable in the current environment, so this suite is
/// compile-only. It is executed in CI where a container runtime exists.
/// </summary>
public sealed class AuditPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("communityos_audit")
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

    private AuditDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(_connectionString!, npgsql => npgsql
                .MigrationsAssembly(typeof(AuditDbContext).Assembly.FullName))
            .Options;
        return new AuditDbContext(options);
    }

    private static AuditIngestor CreateIngestor(AuditDbContext db)
    {
        IRetentionPolicy policy = new AuditRetentionPolicy(
            Options.Create(new AuditOptions
            {
                Retention = new AuditRetentionOptions
                {
                    Classes = new Dictionary<string, string?> { ["default"] = null, ["operational-3y"] = "P3Y" },
                    EventClasses = new Dictionary<string, string> { ["WorkflowTaskCreated"] = "operational-3y" }
                }
            }));
        return new AuditIngestor(db, policy, NullLogger<AuditIngestor>.Instance);
    }

    private async Task<List<string>> QueryStringsAsync(string sql)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var results = new List<string>();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }

    private async Task<int> ExecuteAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var parameter in parameters)
        {
            cmd.Parameters.Add(parameter);
        }

        return await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Migrations_create_all_audit_tables_in_audit_schema()
    {
        var tables = await QueryStringsAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'audit' ORDER BY tablename;");

        tables.Should().Contain(
        [
            "audit_entries", "audit_entry_holds", "organization_unit_references",
            "InboxState", "OutboxMessage", "OutboxState"
        ]);
    }

    [Fact]
    public async Task Immutability_triggers_exist_and_block_unguarded_mutations()
    {
        var ingestor = CreateIngestor(await OpenFreshContext());
        var entryId = Guid.NewGuid();
        await ingestor.AppendAsync(NewEntry(entryId), CancellationToken.None);

        var triggers = await QueryStringsAsync(
            """
            SELECT tgname FROM pg_trigger t
            JOIN pg_class c ON c.oid = t.tgrelid
            WHERE c.relname = 'audit_entries' AND NOT t.tgisinternal;
            """);
        triggers.Should().Contain(["trg_audit_entries_no_update", "trg_audit_entries_no_delete"]);

        await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync("UPDATE audit.audit_entries SET action = 'tampered' WHERE id = @id;",
                new NpgsqlParameter("id", entryId)));
        await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync("DELETE FROM audit.audit_entries WHERE id = @id;",
                new NpgsqlParameter("id", entryId)));

        // The ratified guard unlocks deletion for exactly one transaction.
        await ExecuteAsync("""
                            BEGIN;
                            SET LOCAL app.audit_purge_authorized = 'on';
                            DELETE FROM audit.audit_entries WHERE id = @id;
                            COMMIT;
                            """, new NpgsqlParameter("id", entryId));
    }

    [Fact]
    public async Task Ingestion_is_idempotent_by_source_event_hash()
    {
        await using var db = CreateContext();
        var ingestor = CreateIngestor(db);
        var occurredOn = DateTime.UtcNow;

        var candidate = new IngestCandidate(
            "workflow", "WorkflowTaskCreated", "workflow-task-created",
            "workflow-task", Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(),
            AuditSensitivity.Normal, null,
            AuditMetadata.Create(new Dictionary<string, object?> { ["definition_code"] = "review" }),
            occurredOn,
            SourceEventHash.Compute("workflow", "WorkflowTaskCreated", "workflow-task",
                Guid.NewGuid(), null, occurredOn, "d1"));

        (await ingestor.IngestAsync(candidate, CancellationToken.None)).Should().Be(IngestOutcome.Persisted);
        (await ingestor.IngestAsync(candidate, CancellationToken.None)).Should().Be(IngestOutcome.Duplicate);

        db.ChangeTracker.Clear();
        db.AuditEntries.Count(e => e.SourceEventHash == candidate.SourceEventHash).Should().Be(1);
    }

    [Fact]
    public async Task Hold_placement_exempts_expired_entries_from_purge_until_release()
    {
        await using var db = CreateContext();
        var ingestor = CreateIngestor(db);
        var journal = new AuditJournal(db, NullLogger<AuditJournal>.Instance);

        // One expired-and-held entry plus one expired-unheld entry.
        var heldEntry = NewEntry(Guid.NewGuid(), expiresOn: DateTime.UtcNow.AddDays(-1));
        var freeEntry = NewEntry(Guid.NewGuid(), expiresOn: DateTime.UtcNow.AddDays(-1));
        await journal.AppendWithHoldsAsync(heldEntry,
            [AuditEntryHold.Create(heldEntry.Id, AuditEntryHold.Legal, "investigation", Guid.NewGuid(), DateTime.UtcNow)],
            CancellationToken.None);
        await journal.AppendAsync(freeEntry, CancellationToken.None);

        var result = await journal.PurgeExpiredBatchAsync(100, PurgeMarker, DateTime.UtcNow, CancellationToken.None);

        result.PurgedCount.Should().Be(1);
        result.RemainingExpired.Should().Be(0);
        db.ChangeTracker.Clear();

        db.AuditEntries.Count(e => e.Id == heldEntry.Id).Should().Be(1, "an active hold overrides expiry");
        db.AuditEntries.Count(e => e.Id == freeEntry.Id).Should().Be(0);
        db.AuditEntries.Single(e => e.SourceEventType == "AuditEntriesPurged").RetentionClass
            .Should().Be("audit-journal");
    }

    [Fact]
    public async Task Released_holds_allow_the_next_purge_to_proceed()
    {
        await using var db = CreateContext();
        var journal = new AuditJournal(db, NullLogger<AuditJournal>.Instance);

        var entry = NewEntry(Guid.NewGuid(), expiresOn: DateTime.UtcNow.AddDays(-1));
        var hold = AuditEntryHold.Create(entry.Id, AuditEntryHold.Administrative, "dispute", Guid.NewGuid(), DateTime.UtcNow);
        await journal.AppendWithHoldsAsync(entry, [hold], CancellationToken.None);

        var first = await journal.PurgeExpiredBatchAsync(100, PurgeMarker, DateTime.UtcNow, CancellationToken.None);
        first.PurgedCount.Should().Be(0, "the active hold shields the expired entry");

        var releaser = Guid.NewGuid();
        db.ChangeTracker.Clear();
        hold = await db.AuditEntryHolds.SingleAsync(h => h.EntryId == entry.Id);
        hold.Release(releaser, DateTime.UtcNow);
        var releaseJournal = AuditEntry.Create("audit", "AuditEntryHoldReleased", "audit-hold-released",
            SourceEventHash.Compute("audit", "AuditEntryHoldReleased", "audit-entry-hold",
                hold.Id, entry.Id, DateTime.UtcNow, $"{releaser:D}|release"),
            "audit-entry-hold", hold.Id, DateTime.UtcNow, DateTime.UtcNow,
            AuditSensitivity.Normal, "audit-journal", secondaryResourceId: entry.Id);
        await journal.UpdateHoldAsync(hold, releaseJournal, CancellationToken.None);

        db.ChangeTracker.Clear();
        var second = await journal.PurgeExpiredBatchAsync(100, PurgeMarker, DateTime.UtcNow, CancellationToken.None);
        second.PurgedCount.Should().Be(1);
        db.ChangeTracker.Clear();
        db.AuditEntries.Count(e => e.Id == entry.Id).Should().Be(0);
    }

    private static AuditEntry PurgeMarker(int count, IReadOnlyList<string> classes) =>
        AuditEntry.Create("audit", "AuditEntriesPurged", "audit-purge",
            SourceEventHash.Compute("audit", "AuditEntriesPurged", "audit-entry",
                Guid.NewGuid(), null, DateTime.UtcNow, $"purge|{count}"),
            "audit-entry", Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow,
            AuditSensitivity.Normal, "audit-journal",
            metadata: AuditMetadata.Create(new Dictionary<string, object?>
            {
                ["purged_count"] = count, ["retention_classes"] = string.Join(",", classes)
            }));

    private static AuditEntry NewEntry(Guid id, DateTime? expiresOn = null) =>
        AuditEntry.Create("records", "RecordVerified", "record-verified",
            SourceEventHash.Compute("records", "RecordVerified", "record", id, null, DateTime.UtcNow, "t"),
            "record", Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow,
            AuditSensitivity.Normal, "default", retentionExpiresOn: expiresOn);

    private async Task<AuditDbContext> OpenFreshContext() => CreateContext();
}
