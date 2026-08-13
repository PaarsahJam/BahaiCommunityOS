using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Entities;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.ValueObjects;
using CommunityOS.Community.Infrastructure.Persistence;
using CommunityOS.Community.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CommunityOS.Community.IntegrationTests.Persistence;

/// <summary>
/// Verifies the EF Core mapping and the AddCommunityLife migration against a
/// real PostgreSQL instance (Testcontainers). Configuration errors
/// (snake_case, owned value objects, child tables) surface here rather than in
/// production.
/// </summary>
public sealed class CommunityLifePersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("communityos_community")
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

    private CommunityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CommunityDbContext>()
            .UseNpgsql(_connectionString!, npgsql => npgsql
                .MigrationsAssembly(typeof(CommunityDbContext).Assembly.FullName))
            .Options;
        return new CommunityDbContext(options);
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

    private static PrivacyPreferences DefaultPrivacy() =>
        PrivacyPreferences.Create(ContactVisibility.Public, ContactVisibility.Public, ContactVisibility.Public);

    [Fact]
    public async Task Migrations_create_all_community_life_tables_in_community_schema()
    {
        var tables = await QueryStringsAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'community' ORDER BY tablename;");

        tables.Should().Contain(
        [
            "activities",
            "community_events",
            "contact_methods",
            "family_relationships",
            "household_members",
            "households",
            "meeting_actions",
            "meeting_agenda_items",
            "meeting_participants",
            "meetings",
            "membership_periods",
            "memberships",
            "participations",
            "persons"
        ]);
    }

    [Fact]
    public async Task Migrations_can_be_applied_idempotently()
    {
        await using var db = CreateContext();

        await db.Database.MigrateAsync();

        var tables = await QueryStringsAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'community';");
        tables.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Person_repository_roundtrips_contacts_and_identity_link()
    {
        await using var db = CreateContext();
        var repo = new PersonRepository(db);
        var person = Person.Create("Ruhi Jones", "Ruhi Jones", "en", DefaultPrivacy());
        person.AddContactMethod(ContactMethod.Create(
            ContactMethodType.Email, "ruhi@example.org", true, ContactVisibility.Members));

        await repo.AddAsync(person);

        var fetched = await repo.GetByIdAsync(person.Id);
        fetched.Should().NotBeNull();
        fetched!.PreferredName.Should().Be("Ruhi Jones");
        fetched.Status.Should().Be(PersonStatus.Active.Name);
        fetched.ContactMethods.Should().ContainSingle(m => m.Value == "ruhi@example.org" && m.IsPreferred);

        var accountId = Guid.NewGuid();
        fetched.LinkIdentityAccount(accountId);
        await repo.UpdateAsync(fetched);

        var linked = await repo.GetByIdentityAccountIdAsync(accountId);
        linked.Should().NotBeNull();
        linked!.IdentityAccountId.Should().Be(accountId);

        fetched.UnlinkIdentityAccount();
        await repo.UpdateAsync(fetched);

        var unlinked = await repo.GetByIdAsync(person.Id);
        unlinked!.IdentityAccountId.Should().BeNull();
        unlinked.IdentityUnlinkedOn.Should().NotBeNull();
    }

    [Fact]
    public async Task Household_repository_preserves_members_and_roles()
    {
        await using var db = CreateContext();
        var household = Household.Create(
            "Nabil Street", PostalAddress.Create("1 Nabil St", null, "Haifa", null, "31000", "IL"));
        var person = Guid.NewGuid();
        household.AddMember(person, HouseholdMemberRole.Head, EffectivePeriod.Create(DateTime.UtcNow));

        var repo = new HouseholdRepository(db);
        await repo.AddAsync(household);

        var fetched = await repo.GetByIdAsync(household.Id);
        fetched.Should().NotBeNull();
        fetched!.Members.Should().ContainSingle(m => m.PersonId == person && m.Role == HouseholdMemberRole.Head);
        fetched.Address!.City.Should().Be("Haifa");

        fetched.ChangeMemberRole(person, HouseholdMemberRole.Adult);
        await repo.UpdateAsync(fetched);

        var updated = await repo.GetByIdAsync(household.Id);
        updated!.Members.Should().ContainSingle(m => m.Role == HouseholdMemberRole.Adult);

        (await repo.ListByMemberAsync(person)).Select(h => h.Id).Should().Contain(household.Id);
    }

    [Fact]
    public async Task FamilyRelationship_repository_preserves_active_window()
    {
        await using var db = CreateContext();
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var relationship = FamilyRelationship.Create(
            parent, child, RelationshipType.Parent, EffectivePeriod.Create(now.AddDays(-10), now.AddDays(10)));

        var repo = new FamilyRelationshipRepository(db);
        await repo.AddAsync(relationship);

        var fetched = (await repo.ListByPersonAsync(child)).Single();
        fetched.IsActiveAt(DateTime.UtcNow).Should().BeTrue();
        fetched.RelationshipType.Should().Be(RelationshipType.Parent);

        (await repo.ExistsActiveAsync(parent, child, RelationshipType.Parent)).Should().BeTrue();

        fetched.End(now.AddDays(1));
        await repo.UpdateAsync(fetched);

        var ended = await repo.GetByIdAsync(relationship.Id);
        ended!.IsActiveAt(DateTime.UtcNow).Should().BeFalse();
        (await repo.ExistsActiveAsync(parent, child, RelationshipType.Parent)).Should().BeFalse();
    }

    [Fact]
    public async Task Membership_repository_preserves_period_history()
    {
        await using var db = CreateContext();
        var person = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var membership = Membership.Create(person, MembershipStatus.Active, EffectivePeriod.Create(now));

        var repo = new MembershipRepository(db);
        await repo.AddAsync(membership);

        membership.ChangeStatus(MembershipStatus.Suspended, now.AddDays(1));
        await repo.UpdateAsync(membership);

        var fetched = await repo.GetByPersonAsync(person);
        fetched.Should().NotBeNull();
        fetched!.Status.Should().Be(MembershipStatus.Suspended);
        fetched.PeriodHistory.Should().HaveCount(2);
        fetched.IsEffectiveAt(MembershipStatus.Suspended, now.AddDays(2)).Should().BeTrue();
    }

    [Fact]
    public async Task Activity_repository_roundtrips_schedule_and_state()
    {
        await using var db = CreateContext();
        var orgUnit = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var activity = Activity.Create(
            "Devotional Gathering",
            "Sunday devotional",
            "devotional",
            organizerPersonId: null,
            organizationUnitId: orgUnit,
            location: "Community Hall",
            isOnline: false,
            onlineUrl: null,
            schedule: DateTimeRange.Create(now.AddDays(7), now.AddDays(7).AddHours(2)),
            visibility: ActivityVisibility.Public,
            status: ActivityStatus.Planned,
            capacity: 30);

        var repo = new ActivityRepository(db);
        await repo.AddAsync(activity);

        var fetched = await repo.GetByIdAsync(activity.Id);
        fetched.Should().NotBeNull();
        fetched!.Title.Should().Be("Devotional Gathering");
        fetched.Schedule.StartsAt.Should().BeCloseTo(now.AddDays(7), TimeSpan.FromSeconds(5));
        fetched.OrganizationUnitId.Should().Be(orgUnit);

        fetched.ChangeStatus(ActivityStatus.Active);
        await repo.UpdateAsync(fetched);

        var listed = (await repo.ListAsync(
            from: now.AddDays(7).AddMinutes(-1), through: now.AddDays(7).AddHours(1), organizationUnitId: orgUnit))
            .Single(a => a.Id == activity.Id);
        listed.Status.Should().Be(ActivityStatus.Active);
    }

    [Fact]
    public async Task CommunityEvent_repository_preserves_registration_state()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        var communityEvent = CommunityEvent.Create(
            "Ridvan Celebration",
            description: "Feast of Ridvan",
            startsAt: now.AddDays(14),
            endsAt: now.AddDays(14).AddHours(3),
            timeZone: "Asia/Jerusalem",
            location: "Garden",
            isOnline: false,
            onlineUrl: null,
            organizerPersonId: null,
            organizationUnitId: Guid.NewGuid(),
            status: CommunityEventStatus.Scheduled,
            visibility: CommunityEventVisibility.Members,
            registrationOpen: true,
            capacity: 100);

        var repo = new CommunityEventRepository(db);
        await repo.AddAsync(communityEvent);

        var fetched = await repo.GetByIdAsync(communityEvent.Id);
        fetched.Should().NotBeNull();
        fetched!.RegistrationOpen.Should().BeTrue();
        fetched.Visibility.Should().Be(CommunityEventVisibility.Members);

        fetched.UpdateDetails(
            "Ridvan Celebration", "Feast of Ridvan", "Garden", false, null,
            CommunityEventVisibility.Members, registrationOpen: false);
        await repo.UpdateAsync(fetched);

        var updated = await repo.GetByIdAsync(communityEvent.Id);
        updated!.RegistrationOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Meeting_repository_preserves_participants_agenda_and_actions()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        var meeting = Meeting.Create(
            "Nineteen Day Feast",
            description: "Devotional portion",
            startsAt: now.AddDays(3),
            endsAt: now.AddDays(3).AddHours(1),
            timeZone: "Asia/Jerusalem",
            location: "Hall",
            organizerPersonId: null,
            organizationUnitId: Guid.NewGuid(),
            status: MeetingStatus.Scheduled,
            visibility: MeetingVisibility.Members);

        var member = Guid.NewGuid();
        meeting.AddParticipant(member, "chair");
        meeting.AddAgendaItem("Devotional", null, 1);
        meeting.AddAction("Prepare consultation", member, now.AddDays(2));

        var repo = new MeetingRepository(db);
        await repo.AddAsync(meeting);

        var fetched = await repo.GetByIdAsync(meeting.Id);
        fetched.Should().NotBeNull();
        fetched!.Participants.Should().ContainSingle(p => p.PersonId == member && p.Role == "chair");
        fetched.AgendaItems.Should().ContainSingle(i => i.Title == "Devotional");
        fetched.Actions.Should().ContainSingle(a => a.Description == "Prepare consultation");

        fetched.RecordMinutes("Minutes draft.");
        await repo.UpdateAsync(fetched);

        var recorded = await repo.GetByIdAsync(meeting.Id);
        recorded!.Minutes.Should().Be("Minutes draft.");
        recorded.Status.Should().Be(MeetingStatus.Completed);
    }

    [Fact]
    public async Task Participation_repository_detects_duplicates_and_lists_by_target()
    {
        await using var db = CreateContext();
        var person = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var participation = Participation.Create(
            person, ParticipationTargetType.Activity, activityId, "coordinator",
            ParticipationStatus.Registered, EffectivePeriod.Create(now));

        var repo = new ParticipationRepository(db);
        await repo.AddAsync(participation);

        (await repo.ExistsAsync(person, ParticipationTargetType.Activity, activityId)).Should().BeTrue();
        (await repo.ExistsAsync(person, ParticipationTargetType.Event, activityId)).Should().BeFalse();

        var byTarget = await repo.ListByTargetAsync(ParticipationTargetType.Activity, activityId);
        byTarget.Should().ContainSingle(p => p.PersonId == person);

        participation.MarkAttended();
        await repo.UpdateAsync(participation);

        var attended = (await repo.ListByPersonAsync(person)).Single();
        attended.Status.Should().Be(ParticipationStatus.Attended);
    }
}
