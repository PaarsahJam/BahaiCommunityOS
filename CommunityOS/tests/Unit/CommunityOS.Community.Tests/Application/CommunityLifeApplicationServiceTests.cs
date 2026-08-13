using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Community.Application;
using CommunityOS.Community.Application.Commands;
using CommunityOS.Community.Application.Permissions;
using CommunityOS.Community.Application.Queries;
using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Entities;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CommunityOS.Community.Tests.Application;

public class CommunityLifeApplicationServiceTests
{
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private sealed record RepoSet(
        IPersonRepository Persons,
        IHouseholdRepository Households,
        IFamilyRelationshipRepository FamilyRelationships,
        IMembershipRepository Memberships,
        IActivityRepository Activities,
        ICommunityEventRepository CommunityEvents,
        IMeetingRepository Meetings,
        IParticipationRepository Participations,
        IAuthorizationEvaluator Evaluator)
    {
        public static RepoSet Create()
        {
            var set = new RepoSet(
                Substitute.For<IPersonRepository>(),
                Substitute.For<IHouseholdRepository>(),
                Substitute.For<IFamilyRelationshipRepository>(),
                Substitute.For<IMembershipRepository>(),
                Substitute.For<IActivityRepository>(),
                Substitute.For<ICommunityEventRepository>(),
                Substitute.For<IMeetingRepository>(),
                Substitute.For<IParticipationRepository>(),
                Substitute.For<IAuthorizationEvaluator>());
            set.Evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
                .Returns(call => AuthorizationDecision.Allow(
                    "test-decision", ["test-policy"], DateTime.UtcNow));
            return set;
        }
    }

    private sealed class Harness
    {
        public RepoSet Repos { get; } = RepoSet.Create();
        public ServiceProvider Provider { get; }

        public Harness()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
            services.AddCommunityApplication();

            services.AddScoped(_ => Repos.Persons);
            services.AddScoped(_ => Repos.Households);
            services.AddScoped(_ => Repos.FamilyRelationships);
            services.AddScoped(_ => Repos.Memberships);
            services.AddScoped(_ => Repos.Activities);
            services.AddScoped(_ => Repos.CommunityEvents);
            services.AddScoped(_ => Repos.Meetings);
            services.AddScoped(_ => Repos.Participations);
            services.AddScoped(_ => Repos.Evaluator);
            services.AddScoped<AuthorizationGuard>();

            Provider = services.BuildServiceProvider();
        }

        public ISender Sender => Provider.GetRequiredService<ISender>();
    }

    private static Harness CreateHarness() => new();

    private static Person StubPerson() => Person.Create(
        "Mona", null, null,
        PrivacyPreferences.Create(ContactVisibility.Public, ContactVisibility.Public, ContactVisibility.Public));

    private static ContactMethod ContactMethodInput(string value) =>
        ContactMethod.Create(ContactMethodType.Email, value, true, ContactVisibility.Private);

    [Fact]
    public async Task CreatePerson_persists_and_returns_dto()
    {
        var h = CreateHarness();

        var result = await h.Sender.Send(new CreatePersonCommand(
            ActorId, "Mona", "Mona Smith", "en",
            "public", "members", "private"));

        result.Id.Should().NotBeEmpty();
        result.PreferredName.Should().Be("Mona");
        result.HasLinkedIdentityAccount.Should().BeFalse();
        await h.Repos.Persons.Received(1).AddAsync(Arg.Any<Person>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePerson_requires_create_permission()
    {
        var h = CreateHarness();
        h.Repos.Evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.NoPermission, DateTime.UtcNow));

        var act = () => h.Sender.Send(new CreatePersonCommand(
            ActorId, "Mona", null, null, "public", "public", "public"));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Persons.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task GetPersonById_masks_contact_without_grant()
    {
        var h = CreateHarness();
        var person = StubPerson();
        person.SetContactMethods([ContactMethodInput("a@example.com")]);
        h.Repos.Persons.GetByIdAsync(person.Id, Arg.Any<CancellationToken>()).Returns(person);

        // Only the base person read is granted — contact/sensitive are denied.
        h.Repos.Evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<AuthorizationRequest>();
                return request.Permission == CommunityPermissions.PersonRead
                    ? AuthorizationDecision.Allow("dec", ["read"], DateTime.UtcNow)
                    : AuthorizationDecision.Deny("dec", AuthorizationDecisionReason.NoPermission, DateTime.UtcNow);
            });

        var result = await h.Sender.Send(new GetPersonByIdQuery(ActorId, person.Id));

        result.ContactMethods.Should().BeEmpty();
        result.DateOfBirth.Should().BeNull();
        result.IdentityAccountId.Should().BeNull();
    }

    [Fact]
    public async Task GetPersonById_includes_contact_with_grants()
    {
        var h = CreateHarness();
        var person = StubPerson();
        person.SetContactMethods([ContactMethodInput("a@example.com")]);
        h.Repos.Persons.GetByIdAsync(person.Id, Arg.Any<CancellationToken>()).Returns(person);

        // Grant every requested permission by echoing the request's permission.
        h.Repos.Evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<AuthorizationRequest>();
                return AuthorizationDecision.Allow("test-decision", [request.Permission], DateTime.UtcNow);
            });

        var result = await h.Sender.Send(new GetPersonByIdQuery(ActorId, person.Id));

        result.ContactMethods.Should().ContainSingle(m => m.Value == "a@example.com");
        await h.Repos.Evaluator.Received(1)
            .EvaluateAsync(Arg.Is<AuthorizationRequest>(r =>
                r.SubjectId == ActorId &&
                r.Permission == CommunityPermissions.PersonRead), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LinkIdentity_requires_identity_link_permission()
    {
        var h = CreateHarness();
        var person = StubPerson();
        h.Repos.Persons.GetByIdAsync(person.Id, Arg.Any<CancellationToken>()).Returns(person);

        await h.Sender.Send(new LinkPersonToIdentityAccountCommand(ActorId, person.Id, Guid.NewGuid()));

        await h.Repos.Evaluator.Received(1)
            .EvaluateAsync(Arg.Is<AuthorizationRequest>(r =>
                r.Permission == CommunityPermissions.PersonIdentityLink), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePersonProfile_throws_when_absent()
    {
        var h = CreateHarness();

        var act = () => h.Sender.Send(new UpdatePersonProfileCommand(
            ActorId, Guid.NewGuid(), "New", null, null, null));

        await act.Should().ThrowAsync<PersonNotFoundException>();
    }

    [Fact]
    public async Task CreateMembership_rejects_duplicate_person()
    {
        var h = CreateHarness();
        var personId = Guid.NewGuid();
        h.Repos.Memberships.GetByPersonAsync(personId, Arg.Any<CancellationToken>())
            .Returns(Membership.Create(personId, MembershipStatus.Active, EffectivePeriod.Create(Now)));

        var act = () => h.Sender.Send(new CreateMembershipCommand(
            ActorId, personId, "active", Now));

        await act.Should().ThrowAsync<DuplicateMembershipException>();
    }

    [Fact]
    public async Task CreateFamilyRelationship_rejects_duplicate_active()
    {
        var h = CreateHarness();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        h.Repos.FamilyRelationships.ExistsActiveAsync(a, b, RelationshipType.Parent, Arg.Any<CancellationToken>())
            .Returns(true);

        var act = () => h.Sender.Send(new CreateFamilyRelationshipCommand(
            ActorId, a, b, "parent", Now));

        await act.Should().ThrowAsync<DuplicateFamilyRelationshipException>();
    }

    [Fact]
    public async Task RecordParticipation_rejects_duplicate()
    {
        var h = CreateHarness();
        var personId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        h.Repos.Participations.ExistsAsync(personId, ParticipationTargetType.Event, targetId, Arg.Any<CancellationToken>())
            .Returns(true);

        var act = () => h.Sender.Send(new RecordParticipationCommand(
            ActorId, personId, "event", targetId, null, "registered", Now));

        await act.Should().ThrowAsync<DuplicateParticipationException>();
    }

    [Fact]
    public async Task GetCalendarQuery_requires_calendar_permission()
    {
        var h = CreateHarness();
        h.Repos.Evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<AuthorizationRequest>();
                return AuthorizationDecision.Allow("test-decision", [request.Permission], DateTime.UtcNow);
            });

        var result = await h.Sender.Send(new GetCalendarQuery(
            ActorId, Now, Now.AddDays(30), null, null));

        result.Should().BeEmpty();
        await h.Repos.Evaluator.Received(1)
            .EvaluateAsync(Arg.Is<AuthorizationRequest>(r =>
                r.Permission == CommunityPermissions.CalendarRead), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddMeetingParticipant_uses_manage_permission()
    {
        var h = CreateHarness();
        var meeting = Meeting.Create(
            "Feast", null, Now, Now.AddHours(1), "UTC", null,
            null, null, MeetingStatus.Scheduled, MeetingVisibility.Members);
        h.Repos.Meetings.GetByIdAsync(meeting.Id, Arg.Any<CancellationToken>()).Returns(meeting);

        await h.Sender.Send(new AddMeetingParticipantCommand(ActorId, meeting.Id, Guid.NewGuid(), "member"));

        await h.Repos.Evaluator.Received(1)
            .EvaluateAsync(Arg.Is<AuthorizationRequest>(r =>
                r.Permission == CommunityPermissions.MeetingParticipantManage), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordMeetingMinutes_uses_record_permission()
    {
        var h = CreateHarness();
        var meeting = Meeting.Create(
            "Feast", null, Now, Now.AddHours(1), "UTC", null,
            null, null, MeetingStatus.Scheduled, MeetingVisibility.Members);
        h.Repos.Meetings.GetByIdAsync(meeting.Id, Arg.Any<CancellationToken>()).Returns(meeting);

        await h.Sender.Send(new RecordMeetingMinutesCommand(ActorId, meeting.Id, "draft minutes"));

        await h.Repos.Evaluator.Received(1)
            .EvaluateAsync(Arg.Is<AuthorizationRequest>(r =>
                r.Permission == CommunityPermissions.MeetingRecord), Arg.Any<CancellationToken>());
    }
}
