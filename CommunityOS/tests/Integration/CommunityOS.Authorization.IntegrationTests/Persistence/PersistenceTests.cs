using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Enumerations;
using CommunityOS.Authorization.Domain.Repositories;
using CommunityOS.Authorization.Domain.ValueObjects;
using CommunityOS.Authorization.Infrastructure.Persistence;
using CommunityOS.Authorization.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CommunityOS.Authorization.IntegrationTests.Persistence;

/// <summary>
/// Verifies the EF Core mapping and migrations against a real PostgreSQL
/// instance (Testcontainers). Each test runs against the migrated schema so
/// configuration errors (snake_case, owned scopes, primitive collections)
/// surface here rather than in production.
/// </summary>
public sealed class PersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("communityos_authorization")
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

    private AuthorizationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AuthorizationDbContext>()
            .UseNpgsql(_connectionString!, npgsql => npgsql
                .MigrationsAssembly(typeof(AuthorizationDbContext).Assembly.FullName))
            .Options;
        return new AuthorizationDbContext(options);
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
    public async Task Migrations_create_all_authorization_tables_in_authorization_schema()
    {
        var tables = await QueryStringsAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'authorization' ORDER BY tablename;");

        tables.Should().Contain(
        [
            "authorization_relationships",
            "break_glass_requests",
            "delegations",
            "role_assignments",
            "roles"
        ]);
    }

    [Fact]
    public async Task Migrations_can_be_applied_idempotently()
    {
        await using var db = CreateContext();

        await db.Database.MigrateAsync();

        var tables = await QueryStringsAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'authorization';");
        tables.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Role_repository_roundtrips_permissions()
    {
        await using var db = CreateContext();
        var repo = new RoleRepository(db);
        var role = Role.Create("archivist", "Archivist", "Manages archives", ["records.record.read", "records.record.update"]);

        await repo.AddAsync(role);

        var fetched = await repo.GetByCodeAsync("archivist");
        fetched.Should().NotBeNull();
        fetched!.Permissions.Should().BeEquivalentTo(["records.record.read", "records.record.update"]);
        fetched.Enabled.Should().BeTrue();

        fetched.UpdateDetails("Senior Archivist", "Oversight of archives");
        fetched.UpdatePermissions(["records.record.read", "records.record.verify"]);
        await repo.UpdateAsync(fetched);

        var updated = await repo.GetByIdAsync(role.Id);
        updated!.DisplayName.Should().Be("Senior Archivist");
        updated.Permissions.Should().BeEquivalentTo(["records.record.read", "records.record.verify"]);
    }

    [Fact]
    public async Task RoleAssignment_repository_preserves_scope_and_effective_window()
    {
        await using var db = CreateContext();
        var roleRepo = new RoleRepository(db);
        var role = Role.Create("editor", "Editor", null, ["records.record.read"]);
        await roleRepo.AddAsync(role);

        var subject = Guid.NewGuid();
        var org = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var assignment = RoleAssignment.Create(
            subject, role.Id, role.Code, AuthorizationScope.Scoped(ScopeType.Local, org),
            actor, now, now.AddHours(-1), now.AddDays(7), "onboarding");

        var repo = new RoleAssignmentRepository(db);
        await repo.AddAsync(assignment);

        var fetched = (await repo.ListBySubjectAsync(subject)).Single();
        fetched.Scope.Type.Should().Be(ScopeType.Local);
        fetched.Scope.ScopeId.Should().Be(org);
        fetched.EffectiveFrom.Should().BeCloseTo(now.AddHours(-1), TimeSpan.FromSeconds(5));
        fetched.EffectiveUntil.Should().BeCloseTo(now.AddDays(7), TimeSpan.FromSeconds(5));
        fetched.IsEffectiveAt(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public async Task RoleAssignment_repository_preserves_resource_scope()
    {
        await using var db = CreateContext();
        var roleRepo = new RoleRepository(db);
        var role = Role.Create("editor", "Editor", null, ["records.record.read"]);
        await roleRepo.AddAsync(role);

        var subject = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        var assignment = RoleAssignment.Create(
            subject, role.Id, role.Code, AuthorizationScope.Resource("record", recordId),
            subject, DateTime.UtcNow, null, null, null);

        var repo = new RoleAssignmentRepository(db);
        await repo.AddAsync(assignment);

        var fetched = (await repo.ListBySubjectAsync(subject)).Single();
        fetched.Scope.Type.Should().Be(ScopeType.Resource);
        fetched.Scope.ResourceType.Should().Be("record");
        fetched.Scope.ScopeId.Should().Be(recordId);
    }

    [Fact]
    public async Task Delegation_repository_preserves_permissions_and_period()
    {
        await using var db = CreateContext();
        var delegator = Guid.NewGuid();
        var delegatee = Guid.NewGuid();
        var org = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var delegation = Delegation.Create(
            delegator, delegatee, ["records.record.read"], AuthorizationScope.Scoped(ScopeType.Local, org),
            now.AddHours(-1), now.AddDays(3), "on leave");

        var repo = new DelegationRepository(db);
        await repo.AddAsync(delegation);

        var fetched = (await repo.ListByDelegateAsync(delegatee)).Single();
        fetched.GrantsPermission("records.record.read").Should().BeTrue();
        fetched.IsActiveAt(DateTime.UtcNow).Should().BeTrue();
        fetched.Scope.ScopeId.Should().Be(org);
    }

    [Fact]
    public async Task BreakGlassRequest_repository_preserves_state()
    {
        await using var db = CreateContext();
        var requester = Guid.NewGuid();
        var approver = Guid.NewGuid();
        var org = Guid.NewGuid();
        var request = BreakGlassRequest.Create(
            requester, AuthorizationScope.Scoped(ScopeType.Local, org), ["records.record.read"],
            "emergency", TimeSpan.FromMinutes(30));

        var repo = new BreakGlassRequestRepository(db);
        await repo.AddAsync(request);

        request.Approve(approver, DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await repo.UpdateAsync(request);

        var fetched = (await repo.ListByRequesterAsync(requester)).Single();
        fetched.State.Should().Be(BreakGlassRequestState.Approved);
        fetched.ApproverId.Should().Be(approver);
        fetched.IsActiveAt(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public async Task Relationship_repository_roundtrips_permissions()
    {
        await using var db = CreateContext();
        var subject = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        var relationship = AuthorizationRelationship.Create(
            subject, "has_permission", "record", recordId, ["records.record.read"]);

        var repo = new AuthorizationRelationshipRepository(db);
        await repo.AddAsync(relationship);

        var fetched = (await repo.ListAsync(subjectId: subject)).Single();
        fetched.References(subject, "has_permission", "record", recordId).Should().BeTrue();
        fetched.GrantsPermission("records.record.read").Should().BeTrue();
    }

    [Fact]
    public async Task Seeder_creates_full_role_catalog()
    {
        await using var db = CreateContext();
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        await AuthorizationSeeder.SeedDevelopmentRolesAsync(db, config);

        var codes = await db.Roles.AsNoTracking().Select(r => r.Code).ToListAsync();
        codes.Should().Contain(
        [
            "GlobalAdministrator", "PlatformService", "NationalAdministrator", "LocalAdministrator",
            "CommitteeMember", "Volunteer", "Member", "Guest"
        ]);
    }

    [Fact]
    public async Task Seeder_does_not_create_assignments_without_bootstrap_config()
    {
        await using var db = CreateContext();
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        await AuthorizationSeeder.SeedDevelopmentRolesAsync(db, config);

        (await db.RoleAssignments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Seeder_creates_bootstrap_global_admin_when_configured()
    {
        await using var db = CreateContext();
        var subjectId = Guid.NewGuid();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization:BootstrapGlobalAdminSubjectId"] = subjectId.ToString()
            })
            .Build();

        await AuthorizationSeeder.SeedDevelopmentRolesAsync(db, config);

        var assignment = (await db.RoleAssignments.ToListAsync()).Single();
        assignment.SubjectId.Should().Be(subjectId);
        assignment.IsRevoked.Should().BeFalse();
        assignment.Scope.IsGlobal.Should().BeTrue();
        assignment.RoleCode.Should().Be("GlobalAdministrator");
    }

    [Fact]
    public async Task Seeder_is_idempotent()
    {
        await using var db = CreateContext();
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        await AuthorizationSeeder.SeedDevelopmentRolesAsync(db, config);
        await AuthorizationSeeder.SeedDevelopmentRolesAsync(db, config);

        db.Roles.Count(x => x.Code == "GlobalAdministrator").Should().Be(1);
    }
}
