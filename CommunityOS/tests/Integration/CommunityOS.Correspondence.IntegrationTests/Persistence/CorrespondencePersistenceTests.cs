using CommunityOS.Correspondence.Application;
using CommunityOS.Correspondence.Domain;
using CommunityOS.Correspondence.Infrastructure;
using CommunityOS.Correspondence.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace CommunityOS.Correspondence.IntegrationTests.Persistence;

/// <summary>
/// Verifies the EF Core mapping and the InitialCreateCorrespondence migration
/// against a real PostgreSQL instance (Testcontainers): schema layout, the
/// per-unit yearly unique reference constraint, the purge-guard triggers with
/// the SET LOCAL path, tombstone survival after batch deletion (FK-less
/// history), and the recipient kind check constraint (ADR-028 decisions 4 and
/// 13).
///
/// NOTE: Docker is unavailable in the current environment, so this suite is
/// compile-only. It is executed in CI where a container runtime exists.
/// </summary>
public sealed class CorrespondencePersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("communityos_correspondence")
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

    private CorrespondenceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CorrespondenceDbContext>()
            .UseNpgsql(_connectionString!, npgsql => npgsql
                .MigrationsAssembly(typeof(CorrespondenceDbContext).Assembly.FullName))
            .Options;
        return new CorrespondenceDbContext(options);
    }

    private static LetterJournal CreateJournal(CorrespondenceDbContext db) =>
        new(db, NullLogger<LetterJournal>.Instance);

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
    public async Task Migrations_create_all_correspondence_tables_in_the_correspondence_schema()
    {
        var tables = await QueryStringsAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'correspondence' ORDER BY tablename;");

        tables.Should().Contain(
        [
            "letters", "letter_recipients", "letter_status_history", "letter_documents",
            "letter_attachments", "letter_delivery_records", "templates", "letter_holds",
            "export_activity", "organization_unit_references", "InboxState", "OutboxMessage", "OutboxState"
        ]);
    }

    [Fact]
    public async Task Submission_allocates_per_unit_yearly_references_and_enforces_uniqueness()
    {
        var unit = Guid.NewGuid();
        var actor = Guid.NewGuid();

        await using var db = CreateContext();
        var journal = CreateJournal(db);

        Letter NewConfirmed() 
        {
            var letter = Letter.CreateDraft(unit, "general", "S", "B",
                LetterSensitivity.Normal, actor, DateTime.UtcNow);
            letter.AddRecipient(RecipientKind.Person, Guid.NewGuid(), null, null, DateTime.UtcNow);
            letter.Confirm(Guid.NewGuid(), DateTime.UtcNow);
            return letter;
        }

        static Task Published(CancellationToken ct) => Task.CompletedTask;

        var first = NewConfirmed();
        var second = NewConfirmed();
        await journal.SubmitAsync(first, "default", null, Published, CancellationToken.None);
        await journal.SubmitAsync(second, "default", null, Published, CancellationToken.None);

        first.LetterSequence.Should().Be(1);
        second.LetterSequence.Should().Be(2);

        // A different unit restarts its own sequence.
        var otherUnit = NewConfirmed();
        otherUnit.OrganizationUnitId.Should().NotBe(unit);
        await journal.SubmitAsync(otherUnit, "default", null, Published, CancellationToken.None);
        otherUnit.LetterSequence.Should().Be(1);

        // The storage backstop: a duplicate reference is rejected.
        var act = async () =>
        {
            var duplicate = NewConfirmed();
            duplicate.Submit(actor, first.LetterYear!.Value, 1, "default", null, DateTime.UtcNow);
            db.Letters.Add(duplicate);
            await db.SaveChangesAsync();
        };
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Purge_guard_blocks_unguarded_deletes_and_unlocks_only_for_the_purge_transaction()
    {
        var unit = Guid.NewGuid();
        await using var db = CreateContext();
        var journal = CreateJournal(db);
        var actor = Guid.NewGuid();

        var expired = Letter.CreateDraft(unit, "general", "S", "B", LetterSensitivity.Normal, actor, DateTime.UtcNow);
        expired.AddRecipient(RecipientKind.Person, Guid.NewGuid(), null, null, DateTime.UtcNow);
        expired.Confirm(Guid.NewGuid(), DateTime.UtcNow);
        expired.Submit(Guid.NewGuid(), 2026, 1, "default", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

        var fresh = Letter.CreateDraft(unit, "general", "S", "B", LetterSensitivity.Normal, actor, DateTime.UtcNow);
        fresh.AddRecipient(RecipientKind.Person, Guid.NewGuid(), null, null, DateTime.UtcNow);
        fresh.Confirm(Guid.NewGuid(), DateTime.UtcNow);
        fresh.Submit(Guid.NewGuid(), 2026, 2, "default", DateTime.UtcNow.AddYears(5), DateTime.UtcNow);

        db.Letters.AddRange(expired, fresh);
        await db.SaveChangesAsync();

        var triggers = await QueryStringsAsync(
            """
            SELECT tgname FROM pg_trigger t
            JOIN pg_class c ON c.oid = t.tgrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'correspondence' AND NOT t.tgisinternal
              AND tgname LIKE 'trg_%';
            """);
        triggers.Should().Contain(["trg_letters_no_delete"]);

        // Unguarded out-of-band deletion is rejected.
        await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync("DELETE FROM correspondence.letters WHERE id = @id;",
                new NpgsqlParameter("id", expired.Id)));

        // History tampering is always rejected without the guard.
        await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync("UPDATE correspondence.letter_status_history SET reason_code = 'tampered' WHERE letter_id = @id;",
                new NpgsqlParameter("id", expired.Id)));

        // The guarded purge removes exactly the expired batch.
        var result = await journal.PurgeExpiredBatchAsync(100, DateTime.UtcNow, CancellationToken.None);
        result.PurgedCount.Should().Be(1);
        result.RemainingExpired.Should().Be(0);

        db.ChangeTracker.Clear();
        db.Letters.Count(l => l.Id == expired.Id).Should().Be(0);
        db.Letters.Count(l => l.Id == fresh.Id).Should().Be(1, "unexpired letters survive");
    }

    [Fact]
    public async Task Tombstones_survive_batch_deletion_in_the_fk_less_history_table()
    {
        var unit = Guid.NewGuid();
        await using var db = CreateContext();
        var journal = CreateJournal(db);
        var actor = Guid.NewGuid();

        var letter = Letter.CreateDraft(unit, "general", "S", "B", LetterSensitivity.Normal, actor, DateTime.UtcNow);
        letter.AddRecipient(RecipientKind.Person, Guid.NewGuid(), null, null, DateTime.UtcNow);
        letter.Confirm(Guid.NewGuid(), DateTime.UtcNow);
        letter.Submit(Guid.NewGuid(), 2026, 3, "default", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
        db.Letters.Add(letter);
        await db.SaveChangesAsync();

        await journal.PurgeExpiredBatchAsync(10, DateTime.UtcNow, CancellationToken.None);

        db.ChangeTracker.Clear();
        var history = await db.Set<LetterStatusHistory>()
            .Where(h => h.LetterId == letter.Id)
            .OrderBy(h => h.OccurredOn)
            .ToListAsync(CancellationToken.None);

        history.Should().NotBeEmpty("history outlives the purged letter (no FK by design)");
        history.Last().Cause.Should().Be(HistoryCause.PurgeMarker);
        history.Last().ReasonCode.Should().Be("purged");
    }

    [Fact]
    public async Task Recipient_kind_exclusivity_is_enforced_by_check_constraints()
    {
        var unit = Guid.NewGuid();
        await using var db = CreateContext();
        var letter = Letter.CreateDraft(unit, "general", "S", "B", LetterSensitivity.Normal,
            Guid.NewGuid(), DateTime.UtcNow);
        db.Letters.Add(letter);
        await db.SaveChangesAsync();

        // A person-kind row without a person id violates the check constraint.
        await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync("""
                INSERT INTO correspondence.letter_recipients
                    (id, letter_id, kind, person_id, unit_id, display_line, added_on)
                VALUES (@id, @letter, 'person', NULL, NULL, NULL, now());
                """,
                new NpgsqlParameter("id", Guid.NewGuid()),
                new NpgsqlParameter("letter", letter.Id)));
    }
}
