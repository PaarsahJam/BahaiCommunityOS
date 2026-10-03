using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Repositories;
using CommunityOS.Organization.Domain.ValueObjects;
using CommunityOS.Organization.Infrastructure.Persistence;
using CommunityOS.Organization.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CommunityOS.Organization.IntegrationTests.Persistence;

public sealed class OrganizationOwnedKeyRegressionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("communityos_org_regression")
        .WithUsername("communityos")
        .WithPassword("communityos")
        .WithImage("postgres:18.6")
        .Build();

    private string? _connectionString;
    private readonly List<Guid> _createdOrgIds = [];
    private readonly List<Guid> _createdCommitteeIds = [];
    private readonly List<Guid> _createdUnitIds = [];

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await CleanupAsync();
        await _postgres.DisposeAsync();
    }

    private OrganizationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseNpgsql(_connectionString!, npgsql => npgsql
                .MigrationsAssembly(typeof(OrganizationDbContext).Assembly.FullName))
            .Options;
        return new OrganizationDbContext(options);
    }

    private async Task CleanupAsync()
    {
        await using var db = CreateContext();
        foreach (var id in _createdCommitteeIds)
        {
            var entity = await db.Committees.Include(x => x.Members).IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);
            if (entity is not null) db.Committees.Remove(entity);
        }
        foreach (var id in _createdUnitIds)
        {
            var entity = await db.OrganizationUnits.Include(x => x.Parents).IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);
            if (entity is not null) db.OrganizationUnits.Remove(entity);
        }
        foreach (var id in _createdOrgIds)
        {
            var entity = await db.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);
            if (entity is not null) db.Organizations.Remove(entity);
        }
        await db.SaveChangesAsync();
        _createdOrgIds.Clear();
        _createdCommitteeIds.Clear();
        _createdUnitIds.Clear();
    }

    [Fact]
    public async Task AddCommitteeMember_WithDomainGeneratedGuid_Succeeds()
    {
        await using var db = CreateContext();
        var orgRepo = new OrganizationRepository(db);
        var commRepo = new CommitteeRepository(db);
        
        var org = CommunityOS.Organization.Domain.Aggregates.Organization.Create(
            "Cluster A", "LocalSpiritualAssembly",
            Jurisdiction.Create(CommunityOS.Organization.Domain.Enumerations.JurisdictionType.Local, Guid.NewGuid()));
        await orgRepo.AddAsync(org);
        _createdOrgIds.Add(org.Id);
        
        var committee = Committee.Create(
            "Board", "Executive", org.Id, null,
            Jurisdiction.Create(CommunityOS.Organization.Domain.Enumerations.JurisdictionType.Local, Guid.NewGuid()));
        await commRepo.AddAsync(committee);
        _createdCommitteeIds.Add(committee.Id);
        
        committee.AddMember(Guid.NewGuid(), "Chair", EffectivePeriod.Create(DateTime.UtcNow));
        await commRepo.UpdateAsync(committee);
        
        var fetched = await commRepo.GetByIdAsync(committee.Id);
        fetched.Should().NotBeNull();
        fetched!.Members.Should().HaveCount(1);
        var memberId = fetched.Members[0].Id;
        memberId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AddOrganizationUnitParent_WithDomainGeneratedGuid_Succeeds()
    {
        await using var db = CreateContext();
        var orgRepo = new OrganizationRepository(db);
        var unitRepo = new OrganizationUnitRepository(db);
        
        var org = CommunityOS.Organization.Domain.Aggregates.Organization.Create(
            "Cluster B", "LocalSpiritualAssembly",
            Jurisdiction.Create(CommunityOS.Organization.Domain.Enumerations.JurisdictionType.Local, Guid.NewGuid()));
        await orgRepo.AddAsync(org);
        _createdOrgIds.Add(org.Id);
        
        var now = DateTime.UtcNow;
        var unit = OrganizationUnit.Create(
            org.Id, "Unit A", "Institute", null, EffectivePeriod.Create(now));
        await unitRepo.AddAsync(unit);
        _createdUnitIds.Add(unit.Id);
        
        var parentId = Guid.NewGuid();
        unit.ChangeParent(parentId, EffectivePeriod.Create(now.AddDays(1)));
        await unitRepo.UpdateAsync(unit);
        
        var fetched = await unitRepo.GetByIdAsync(unit.Id);
        fetched.Should().NotBeNull();
        fetched!.Parents.Should().HaveCount(2);
        var lastParent = fetched.Parents[^1];
        lastParent.Id.Should().NotBeEmpty();
        lastParent.ParentId.Should().Be(parentId);
    }
}
