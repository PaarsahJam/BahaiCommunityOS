using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Enumerations;
using CommunityOS.Organization.Domain.Repositories;
using CommunityOS.Organization.Domain.ValueObjects;
using CommunityOS.Organization.Infrastructure.Persistence;
using CommunityOS.Organization.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CommunityOS.Organization.IntegrationTests.Persistence;

using Organization = CommunityOS.Organization.Domain.Aggregates.Organization;

/// <summary>
/// Verifies the EF Core mapping and migrations against a real PostgreSQL
/// instance (Testcontainers). Each test runs against the migrated schema so
/// configuration errors (snake_case, owned value objects, parent history
/// tables) surface here rather than in production.
/// </summary>
public sealed class OrganizationPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("communityos_organization")
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

    private OrganizationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseNpgsql(_connectionString!, npgsql => npgsql
                .MigrationsAssembly(typeof(OrganizationDbContext).Assembly.FullName))
            .Options;
        return new OrganizationDbContext(options);
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
    public async Task Migrations_create_all_organization_tables_in_organization_schema()
    {
        var tables = await QueryStringsAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'organization' ORDER BY tablename;");

        tables.Should().Contain(
        [
            "appointments",
            "committees",
            "committee_members",
            "delegation_facts",
            "institutions",
            "organizations",
            "organization_units",
            "organization_unit_parents"
        ]);
    }

    [Fact]
    public async Task Migrations_can_be_applied_idempotently()
    {
        await using var db = CreateContext();

        await db.Database.MigrateAsync();

        var tables = await QueryStringsAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'organization';");
        tables.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Organization_repository_roundtrips_organization()
    {
        await using var db = CreateContext();
        var repo = new OrganizationRepository(db);
        var organization = Organization.Create(
            "Ridvan Cluster", "LocalSpiritualAssembly",
            Jurisdiction.Create(JurisdictionType.Local, Guid.NewGuid()));

        await repo.AddAsync(organization);

        var fetched = await repo.GetByIdAsync(organization.Id);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("Ridvan Cluster");
        fetched.Status.Should().Be("active");
        fetched.Jurisdiction.Type.Should().Be(JurisdictionType.Local);

        fetched.UpdateDetails("Ridvan National", Jurisdiction.Create(JurisdictionType.National, Guid.NewGuid()));
        await repo.UpdateAsync(fetched);

        var updated = await repo.GetByIdAsync(organization.Id);
        updated!.Name.Should().Be("Ridvan National");
        updated.Jurisdiction.Type.Should().Be(JurisdictionType.National);
    }

    [Fact]
    public async Task OrganizationUnit_repository_preserves_parent_history()
    {
        await using var db = CreateContext();
        var org = Organization.Create(
            "Ridvan Cluster", "LocalSpiritualAssembly",
            Jurisdiction.Create(JurisdictionType.Local, Guid.NewGuid()));
        await new OrganizationRepository(db).AddAsync(org);

        var now = DateTime.UtcNow;
        var root = OrganizationUnit.Create(org.Id, "Root", "Cluster", null, EffectivePeriod.Create(now));
        await new OrganizationUnitRepository(db).AddAsync(root);

        var child = OrganizationUnit.Create(org.Id, "Unit A", "Institute", root.Id, EffectivePeriod.Create(now));
        var repo = new OrganizationUnitRepository(db);
        await repo.AddAsync(child);

        var newParent = Guid.NewGuid();
        child.ChangeParent(newParent, EffectivePeriod.Create(now.AddDays(1)));
        await repo.UpdateAsync(child);

        var fetched = await repo.GetByIdAsync(child.Id);
        fetched.Should().NotBeNull();
        fetched!.Parents.Should().HaveCount(2);
        fetched.ParentIdAt(now.AddDays(-1)).Should().Be(root.Id);
        fetched.ParentIdAt(now.AddDays(2)).Should().Be(newParent);

        (await repo.ListRootsAsync(org.Id)).Select(u => u.Id).Should().Contain(root.Id);
        (await repo.ListChildrenAsync(root.Id)).Select(u => u.Id).Should().Contain(child.Id);
        (await repo.ListDescendantsAsync(root.Id)).Select(u => u.Id).Should().Contain(child.Id);
        (await repo.IsDescendantAsync(child.Id, root.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Appointment_repository_preserves_effective_window()
    {
        await using var db = CreateContext();
        var org = Organization.Create(
            "Ridvan Cluster", "LocalSpiritualAssembly",
            Jurisdiction.Create(JurisdictionType.Local, Guid.NewGuid()));
        await new OrganizationRepository(db).AddAsync(org);

        var unit = OrganizationUnit.Create(org.Id, "Unit A", "Institute", null, EffectivePeriod.Create(DateTime.UtcNow));
        await new OrganizationUnitRepository(db).AddAsync(unit);

        var person = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var appointment = Appointment.Create(
            person, unit.Id, "Treasurer", EffectivePeriod.Create(now.AddDays(-30), now.AddDays(30)), org.Id);

        var repo = new AppointmentRepository(db);
        await repo.AddAsync(appointment);

        var fetched = (await repo.ListByPersonAsync(person)).Single();
        fetched.Status.Should().Be(AppointmentStatus.Active);
        fetched.Period.EffectiveFrom.Should().BeCloseTo(now.AddDays(-30), TimeSpan.FromSeconds(5));
        fetched.IsEffectiveAt(DateTime.UtcNow).Should().BeTrue();

        fetched.End(org.Id, "term complete");
        await repo.UpdateAsync(fetched);

        var ended = (await repo.ListAsync(personId: person, organizationUnitId: null, asOf: null, view: AppointmentView.Ended)).Single();
        ended.IsEnded.Should().BeTrue();
    }

    [Fact]
    public async Task Committee_repository_roundtrips_members()
    {
        await using var db = CreateContext();
        var org = Organization.Create(
            "Ridvan Cluster", "LocalSpiritualAssembly",
            Jurisdiction.Create(JurisdictionType.Local, Guid.NewGuid()));
        await new OrganizationRepository(db).AddAsync(org);

        var unit = OrganizationUnit.Create(org.Id, "Unit A", "Institute", null, EffectivePeriod.Create(DateTime.UtcNow));
        await new OrganizationUnitRepository(db).AddAsync(unit);

        var committee = Committee.Create(
            "Teaching Committee", "Teaching", org.Id, unit.Id,
            Jurisdiction.Create(JurisdictionType.OrganizationUnit, unit.Id));
        var person = Guid.NewGuid();
        committee.AddMember(person, "convener", EffectivePeriod.Create(DateTime.UtcNow.AddDays(-10)));

        var repo = new CommitteeRepository(db);
        await repo.AddAsync(committee);

        var fetched = await repo.GetByIdAsync(committee.Id);
        fetched.Should().NotBeNull();
        fetched!.Members.Should().ContainSingle(m => m.PersonId == person && m.RoleCode == "convener");

        fetched.RemoveMember(person, "convener");
        await repo.UpdateAsync(fetched);

        var updated = await repo.GetByIdAsync(committee.Id);
        updated!.Members.Should().BeEmpty();
    }

    [Fact]
    public async Task Institution_repository_roundtrips_institution()
    {
        await using var db = CreateContext();
        var repo = new InstitutionRepository(db);
        var institution = Institution.Create(
            "Universal House of Justice", "WorldCentre", Jurisdiction.Global());

        await repo.AddAsync(institution);

        var fetched = await repo.GetByIdAsync(institution.Id);
        fetched.Should().NotBeNull();
        fetched!.IsActive.Should().BeTrue();
        fetched.Jurisdiction.IsGlobal.Should().BeTrue();

        fetched.Deactivate();
        await repo.UpdateAsync(fetched);

        var updated = await repo.GetByIdAsync(institution.Id);
        updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DelegationFact_repository_preserves_state()
    {
        await using var db = CreateContext();
        var org = Organization.Create(
            "Ridvan Cluster", "LocalSpiritualAssembly",
            Jurisdiction.Create(JurisdictionType.Local, Guid.NewGuid()));
        await new OrganizationRepository(db).AddAsync(org);

        var unit = OrganizationUnit.Create(org.Id, "Unit A", "Institute", null, EffectivePeriod.Create(DateTime.UtcNow));
        await new OrganizationUnitRepository(db).AddAsync(unit);

        var delegator = Guid.NewGuid();
        var delegatee = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var fact = DelegationFact.Create(
            delegator, delegatee, unit.Id, "SigningAuthority",
            EffectivePeriod.Create(now.AddDays(-10), now.AddDays(10)), delegator, "on leave");

        var repo = new DelegationFactRepository(db);
        await repo.AddAsync(fact);

        var fetched = (await repo.ListByDelegateAsync(delegatee)).Single();
        fetched.IsActiveAt(DateTime.UtcNow).Should().BeTrue();
        fetched.OrganizationUnitId.Should().Be(unit.Id);

        fetched.Revoke(delegator, "no longer needed");
        await repo.UpdateAsync(fetched);

        var revoked = await repo.GetByIdAsync(fact.Id);
        revoked!.IsRevoked.Should().BeTrue();
        revoked.RevokedBy.Should().Be(delegator);
    }

    [Fact]
    public async Task Seeder_creates_development_hierarchy_idempotently()
    {
        await using var db = CreateContext();

        await OrganizationSeeder.SeedDevelopmentDataAsync(db);
        await OrganizationSeeder.SeedDevelopmentDataAsync(db);

        (await db.Organizations.CountAsync()).Should().Be(1);
        (await db.OrganizationUnits.CountAsync()).Should().Be(2);
        (await db.Committees.CountAsync()).Should().Be(1);
        (await db.Institutions.CountAsync()).Should().Be(1);
    }
}
