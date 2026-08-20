using CommunityOS.Workflow.Domain.Aggregates;
using CommunityOS.Workflow.Domain.Enumerations;
using CommunityOS.Workflow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CommunityOS.Workflow.IntegrationTests.Persistence;

/// <summary>
/// Verifies the EF Core mapping and the InitialCreateWorkflow migration against
/// a real PostgreSQL instance (Testcontainers). Configuration errors
/// (snake_case, owned child tables, status stored as int, the workflow schema,
/// the filtered unique index, and the MassTransit outbox tables) surface here
/// rather than in production.
///
/// NOTE: Docker is unavailable in the current environment, so this suite is
/// compile-only. It is executed in CI where a container runtime exists.
/// </summary>
public sealed class WorkflowPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("communityos_workflow")
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

    private WorkflowDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseNpgsql(_connectionString!, npgsql => npgsql
                .MigrationsAssembly(typeof(WorkflowDbContext).Assembly.FullName))
            .Options;
        return new WorkflowDbContext(options);
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
    public async Task Migrations_create_all_workflow_tables_in_workflow_schema()
    {
        var tables = await QueryStringsAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'workflow' ORDER BY tablename;");

        tables.Should().Contain(
        [
            "workflow_tasks", "task_assignments", "task_assignment_assignees",
            "task_scopes", "task_activity", "task_definitions", "task_definition_outcomes",
            "organization_unit_references",
            "InboxState", "OutboxMessage", "OutboxState"
        ]);
    }

    [Fact]
    public async Task Task_round_trip_preserves_owned_children_and_activity()
    {
        await using var db = CreateContext();

        var task = WorkflowTask.Create(
            WorkflowDefinitionCodes.RecordReview,
            WorkflowDomainTypes.Record,
            Guid.NewGuid(),
            organizationUnitId: Guid.NewGuid(),
            additionalScopes: [Guid.NewGuid()],
            assigneeIds: [Guid.NewGuid(), Guid.NewGuid()],
            dueOn: null,
            notes: "Sensitive note.",
            originatorId: Guid.NewGuid(),
            createdBy: Guid.NewGuid(),
            occurredOn: DateTime.UtcNow);

        db.WorkflowTasks.Add(task);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = await db.WorkflowTasks
            .Include(t => t.Assignments)
            .ThenInclude(a => a.Assignees)
            .Include(t => t.Scopes)
            .Include(t => t.Activity)
            .SingleAsync(t => t.Id == task.Id);

        loaded.Should().NotBeNull();
        loaded.Assignments.Should().ContainSingle();
        loaded.Assignments.Single().Assignees.Select(a => a.AssigneeId)
            .Should().BeEquivalentTo(task.AssigneeIds);
        loaded.Scopes.Should().HaveCount(task.Scopes.Count);
        loaded.Activity.Should().Contain(a => a.Action == WorkflowActivityActions.Created);
    }

    [Fact]
    public async Task Filtered_unique_index_rejects_a_second_open_task_for_same_domain_reference()
    {
        await using var db = CreateContext();

        var domainEntityId = Guid.NewGuid();
        var first = WorkflowTask.Create(
            WorkflowDefinitionCodes.RecordReview, WorkflowDomainTypes.Record, domainEntityId,
            null, [], [], null, null, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        db.WorkflowTasks.Add(first);
        await db.SaveChangesAsync();

        var second = WorkflowTask.Create(
            WorkflowDefinitionCodes.RecordReview, WorkflowDomainTypes.Record, domainEntityId,
            null, [], [], null, null, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        db.WorkflowTasks.Add(second);
        await db.Invoking(d => d.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }
}
