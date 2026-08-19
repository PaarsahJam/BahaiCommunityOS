using CommunityOS.Records.Domain.Aggregates;
using CommunityOS.Records.Domain.Enumerations;
using CommunityOS.Records.Infrastructure.Persistence;
using CommunityOS.Records.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CommunityOS.Records.IntegrationTests.Persistence;

/// <summary>
/// Verifies the EF Core mapping and the InitialCreateRecords migration against
/// a real PostgreSQL instance (Testcontainers). Configuration errors
/// (snake_case, owned child tables, enums stored as strings, the records
/// schema, and the MassTransit outbox tables) surface here rather than in
/// production.
/// </summary>
public sealed class RecordsPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("communityos_records")
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

    private RecordsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RecordsDbContext>()
            .UseNpgsql(_connectionString!, npgsql => npgsql
                .MigrationsAssembly(typeof(RecordsDbContext).Assembly.FullName))
            .Options;
        return new RecordsDbContext(options);
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
    public async Task Migrations_create_all_records_tables_in_records_schema()
    {
        var tables = await QueryStringsAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'records' ORDER BY tablename;");

        tables.Should().Contain(
        [
            "records",
            "organization_unit_references",
            "record_categories",
            "record_classification",
            "record_evidence_references",
            "record_field_values",
            "record_hold_document_references",
            "record_holds",
            "record_scopes",
            "record_versions",
            "record_working_fields",
            "retention_rules",
            "retention_schedules"
        ]);
    }

    [Fact]
    public async Task Migrations_create_the_masstransit_outbox_tables()
    {
        var tables = await QueryStringsAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'records' ORDER BY tablename;");

        tables.Should().Contain(["InboxState", "OutboxMessage", "OutboxState"]);
    }

    [Fact]
    public async Task Migrations_can_be_applied_idempotently()
    {
        await using var db = CreateContext();

        await db.Database.MigrateAsync();

        var tables = await QueryStringsAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'records';");
        tables.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Record_repository_roundtrip_preserves_lifecycle_versions_and_child_collections()
    {
        await using var db = CreateContext();
        var repo = new RecordRepository(db);

        var actor = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var unitId = Guid.NewGuid();

        var record = Record.Create("membership", RecordSubjectTypes.Person, Guid.NewGuid(), unitId,
            [RecordFieldValue.Create("name", "A Flower", false)], isSensitive: false, actor, now);
        record.Submit(actor, now);
        record.MoveUnderReview(reviewer, now);
        record.Verify(reviewer, now);
        record.AddOrganizationScope(Guid.NewGuid(), actor, now);
        record.AttachEvidence(Guid.NewGuid(), 1, "proof_of_address", actor, now);
        record.PlaceHold("legal", "Subpoena received.", null, actor, now);
        await repo.AddAsync(record);

        var fetched = await repo.GetByIdAsync(record.Id);
        fetched.Should().NotBeNull();
        fetched!.Status.Should().Be(RecordStatus.Verified);
        fetched.CurrentVersion.Should().NotBeNull();
        fetched.CurrentVersion!.VersionNumber.Should().Be(1);
        fetched.WorkingFields.Should().Contain(f => f.FieldKey == "name");
        fetched.Scopes.Should().ContainSingle();
        fetched.Evidence.Should().ContainSingle(e => e.ReferenceType == "proof_of_address");
        fetched.Holds.Should().ContainSingle(h => h.IsActive);

        var byPrimary = await repo.ListByOrganizationUnitAsync(record.OrganizationUnitId!.Value);
        byPrimary.Should().ContainSingle(r => r.Id == record.Id);
        var byScope = await repo.ListByOrganizationUnitAsync(record.Scopes.Single().OrganizationUnitId);
        byScope.Should().ContainSingle(r => r.Id == record.Id);
    }

    [Fact]
    public async Task Record_repository_preserves_append_only_versions_after_correction()
    {
        await using var db = CreateContext();
        var repo = new RecordRepository(db);

        var actor = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var record = Record.Create("membership", RecordSubjectTypes.Person, Guid.NewGuid(), null,
            [RecordFieldValue.Create("name", "A Flower", false)], isSensitive: false, actor, now);
        record.Submit(actor, now);
        record.MoveUnderReview(reviewer, now);
        record.Verify(reviewer, now);
        record.Correct(
            [RecordFieldValue.Create("name", "A Flower", false), RecordFieldValue.Create("address", "New Address", false)],
            "Address corrected after verification.", actor, now);
        await repo.AddAsync(record);

        var fetched = await repo.GetByIdAsync(record.Id);
        fetched!.Versions.Should().HaveCount(2);
        fetched.CurrentVersion!.VersionNumber.Should().Be(2);
        fetched.CurrentVersion.SupersedesVersionNumber.Should().Be(1);
        fetched.Versions[0].Fields.Should().ContainSingle(f => f.FieldKey == "name");
    }

    [Fact]
    public async Task RetentionSchedule_repository_roundtrip_preserves_rules()
    {
        await using var db = CreateContext();
        var repo = new RetentionScheduleRepository(db);

        var schedule = RetentionSchedule.Create(
            "gen-ret-10", "General retention", "Ten-year general schedule",
            [RetentionRule.Create("*", "recordDate", "P10Y", "review", "Review at ten years.", "P20Y")],
            Guid.NewGuid());
        await repo.AddAsync(schedule);

        var fetched = await repo.GetByCodeAsync("gen-ret-10");
        fetched.Should().NotBeNull();
        fetched!.DisplayName.Should().Be("General retention");
        fetched.Rules.Should().ContainSingle(r => r.Disposition == "review");
        fetched.Rules.Single().MaximumPeriod.Should().Be("P20Y");

        fetched.Update("General retention (updated)", null,
            [RetentionRule.Create("*", "recordDate", "P10Y", "review", null, "P25Y")], Guid.NewGuid());
        await repo.UpdateAsync(fetched);

        var updated = await repo.GetByCodeAsync("gen-ret-10");
        updated!.Rules.Should().ContainSingle();
        updated.Rules.Single().MaximumPeriod.Should().Be("P25Y");
    }

    [Fact]
    public async Task RecordCategory_repository_roundtrip()
    {
        await using var db = CreateContext();
        var repo = new RecordCategoryRepository(db);

        var category = RecordCategory.Create("membership", "Membership records", "Enrollment and service records.", Guid.NewGuid());
        await repo.AddAsync(category);

        var fetched = await repo.GetByCodeAsync("membership");
        fetched.Should().NotBeNull();
        fetched!.DisplayName.Should().Be("Membership records");
        (await repo.ExistsByCodeAsync("membership")).Should().BeTrue();
    }

    [Fact]
    public async Task OrganizationUnitReference_repository_upserts_read_model()
    {
        await using var db = CreateContext();
        var repo = new RecordsOrganizationUnitReferenceRepository(db);

        var unitId = Guid.NewGuid();
        var reference = OrganizationUnitReference.Create(unitId, DateTime.UtcNow);
        await repo.AddAsync(reference);

        (await repo.ExistsAsync(unitId)).Should().BeTrue();
        await repo.RemoveAsync(unitId);
        (await repo.ExistsAsync(unitId)).Should().BeFalse();
    }
}