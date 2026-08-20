using CommunityOS.Notifications.Domain.Aggregates;
using CommunityOS.Notifications.Domain.Enumerations;
using CommunityOS.Notifications.Domain.ValueObjects;
using CommunityOS.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CommunityOS.Notifications.IntegrationTests.Persistence;

/// <summary>
/// Verifies the EF Core mapping and the InitialCreate migration against a real
/// PostgreSQL instance (Testcontainers). Configuration errors (snake_case,
/// owned recipient/scope child tables, status stored as int, the
/// <c>notifications</c> schema, the filtered idempotency unique index, and the
/// MassTransit outbox tables) surface here rather than in production.
///
/// NOTE: Docker is unavailable in the current environment, so this suite is
/// compile-only. It is executed in CI where a container runtime exists.
/// </summary>
public sealed class NotificationsPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("communityos_notifications")
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

    private NotificationsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql(_connectionString!, npgsql => npgsql
                .MigrationsAssembly(typeof(NotificationsDbContext).Assembly.FullName))
            .Options;
        return new NotificationsDbContext(options);
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
    public async Task Migrations_create_all_notification_tables_in_notifications_schema()
    {
        var tables = await QueryStringsAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'notifications' ORDER BY tablename;");

        tables.Should().Contain(
        [
            "notifications", "notification_recipients", "notification_scopes",
            "notification_types", "notification_preferences", "organization_unit_references",
            "InboxState", "OutboxMessage", "OutboxState"
        ]);
    }

    [Fact]
    public async Task Baseline_types_seed_idempotently()
    {
        await using var db = CreateContext();
        await NotificationsCatalogSeeder.SeedBaselineTypesAsync(db);

        db.NotificationTypes.Count().Should().Be(12);
        (await db.NotificationTypes.SingleAsync(t => t.Code == "record-hold")).IsSensitive.Should().BeTrue();

        await NotificationsCatalogSeeder.SeedBaselineTypesAsync(db);
        db.NotificationTypes.Count().Should().Be(12);
    }

    [Fact]
    public async Task Notification_round_trip_preserves_owned_recipients_and_scopes()
    {
        await using var db = CreateContext();

        var notification = Notification.Create(
            "task-assigned",
            NotificationChannel.InApp,
            "workflow-task",
            Guid.NewGuid(),
            MessageTemplate.Create("A task has been assigned to you", "Task {{TaskId}} assigned."),
            organizationUnitId: Guid.NewGuid(),
            additionalScopes: [Guid.NewGuid()],
            scheduledFor: null,
            isSensitive: false,
            recipientIds: [Guid.NewGuid(), Guid.NewGuid()],
            createdBy: Guid.NewGuid(),
            occurredOn: DateTime.UtcNow);
        notification.Queue();

        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = await db.Notifications
            .Include(n => n.Recipients)
            .Include(n => n.AdditionalScopes)
            .SingleAsync(n => n.Id == notification.Id);

        loaded.Should().NotBeNull();
        loaded.Status.Should().Be(NotificationLifecycleStatus.Queued);
        loaded.Recipients.Should().HaveCount(2);
        loaded.AdditionalScopes.Should().HaveCount(1);
    }

    [Fact]
    public async Task Filtered_unique_index_rejects_a_second_notification_for_the_same_source_fact()
    {
        await using var db = CreateContext();

        var sourceId = Guid.NewGuid();
        var first = Notification.Create(
            "task-assigned", NotificationChannel.InApp, "workflow-task", sourceId,
            MessageTemplate.Create("s", "b"), null, [], null, false, [Guid.NewGuid()],
            Guid.NewGuid(), DateTime.UtcNow);
        db.Notifications.Add(first);
        await db.SaveChangesAsync();

        var second = Notification.Create(
            "task-assigned", NotificationChannel.InApp, "workflow-task", sourceId,
            MessageTemplate.Create("s", "b"), null, [], null, false, [Guid.NewGuid()],
            Guid.NewGuid(), DateTime.UtcNow);

        db.Notifications.Add(second);
        await db.Invoking(d => d.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }
}