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

namespace CommunityOS.Community.Tests.Security;

/// <summary>
/// Security regression tests. These lock in the privacy and authorization
/// boundaries of the Community bounded context: person/contact/household/
/// membership/activity/meeting reads are gated, sensitive data (contact
/// details, date of birth, identity links, family relationships) is masked
/// without an explicit grant, and the guard is fail-closed.
/// </summary>
public class CommunitySecurityRegressionTests
{
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

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

        /// <summary>Denies every authorization request (fail-closed default).</summary>
        public void DenyAll() => Repos.Evaluator.EvaluateAsync(
                Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.NoPermission, DateTime.UtcNow));

        /// <summary>Allows every authorization request.</summary>
        public void AllowAll() => Repos.Evaluator.EvaluateAsync(
                Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => AuthorizationDecision.Allow(
                "test-decision",
                [call.Arg<AuthorizationRequest>().Permission],
                DateTime.UtcNow));
    }

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
        public static RepoSet Create() => new(
            Substitute.For<IPersonRepository>(),
            Substitute.For<IHouseholdRepository>(),
            Substitute.For<IFamilyRelationshipRepository>(),
            Substitute.For<IMembershipRepository>(),
            Substitute.For<IActivityRepository>(),
            Substitute.For<ICommunityEventRepository>(),
            Substitute.For<IMeetingRepository>(),
            Substitute.For<IParticipationRepository>(),
            Substitute.For<IAuthorizationEvaluator>());
    }

    private static Harness CreateHarness() => new();

    private static Person StubPerson()
    {
        var person = Person.Create(
            "Mona", "Mona Smith", "en",
            PrivacyPreferences.Create(ContactVisibility.Public, ContactVisibility.Public, ContactVisibility.Public));
        person.UpdateProfile("Mona", "Mona Smith", new DateTime(1980, 5, 10), "en");
        person.SetContactMethods([ContactMethod.Create(
            ContactMethodType.Email, "mona@example.com", true, ContactVisibility.Members)]);
        person.LinkIdentityAccount(Guid.NewGuid());
        return person;
    }

    // --- Fail-closed behavior ---

    [Fact]
    public async Task CreatePerson_is_denied_when_evaluator_denies()
    {
        var h = CreateHarness();
        h.DenyAll();

        var act = () => h.Sender.Send(new CreatePersonCommand(
            ActorId, "Mona", null, null, "public", "public", "public"));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Persons.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task CreatePerson_is_denied_when_evaluator_throws()
    {
        var h = CreateHarness();
        h.Repos.Evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AuthorizationDecision>(new HttpRequestException("authz down")));

        var act = () => h.Sender.Send(new CreatePersonCommand(
            ActorId, "Mona", null, null, "public", "public", "public"));

        await act.Should().ThrowAsync<Exception>();
        await h.Repos.Persons.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    // --- Privacy-scoped person reads ---

    [Fact]
    public async Task GetPersonById_masks_contact_sensitive_and_identity_without_grants()
    {
        var h = CreateHarness();
        h.AllowAll();
        var person = StubPerson();
        h.Repos.Persons.GetByIdAsync(person.Id, Arg.Any<CancellationToken>()).Returns(person);

        // Rewire evaluator so only PersonRead is granted, no sensitive grants.
        h.Repos.Evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<AuthorizationRequest>();
                return request.Permission == CommunityPermissions.PersonRead
                    ? AuthorizationDecision.Allow("dec", ["read"], DateTime.UtcNow)
                    : AuthorizationDecision.Deny("dec", AuthorizationDecisionReason.NoPermission, DateTime.UtcNow);
            });

        var result = await h.Sender.Send(new GetPersonByIdQuery(ActorId, person.Id));

        result.PreferredName.Should().Be("Mona");
        result.ContactMethods.Should().BeEmpty();
        result.DateOfBirth.Should().BeNull();
        result.IdentityAccountId.Should().BeNull();
    }

    [Fact]
    public async Task GetPersonById_exposes_contact_only_with_contact_grant()
    {
        var h = CreateHarness();
        var person = StubPerson();
        h.Repos.Persons.GetByIdAsync(person.Id, Arg.Any<CancellationToken>()).Returns(person);

        h.Repos.Evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<AuthorizationRequest>();
                var granted = request.Permission switch
                {
                    CommunityPermissions.PersonRead or CommunityPermissions.PersonContactRead => true,
                    _ => false
                };
                return granted
                    ? AuthorizationDecision.Allow("dec", [request.Permission], DateTime.UtcNow)
                    : AuthorizationDecision.Deny("dec", AuthorizationDecisionReason.NoPermission, DateTime.UtcNow);
            });

        var result = await h.Sender.Send(new GetPersonByIdQuery(ActorId, person.Id));

        result.ContactMethods.Should().ContainSingle(m => m.Value == "mona@example.com");
        result.DateOfBirth.Should().BeNull();
        result.IdentityAccountId.Should().BeNull();
    }

    [Fact]
    public async Task GetPersonById_throws_without_read_grant()
    {
        var h = CreateHarness();
        h.DenyAll();
        var person = StubPerson();
        h.Repos.Persons.GetByIdAsync(person.Id, Arg.Any<CancellationToken>()).Returns(person);

        var act = () => h.Sender.Send(new GetPersonByIdQuery(ActorId, person.Id));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
    }

    // --- Identity link authorization ---

    [Fact]
    public async Task LinkIdentity_throws_without_identity_link_permission()
    {
        var h = CreateHarness();
        h.DenyAll();
        var person = StubPerson();
        h.Repos.Persons.GetByIdAsync(person.Id, Arg.Any<CancellationToken>()).Returns(person);

        var act = () => h.Sender.Send(new LinkPersonToIdentityAccountCommand(ActorId, person.Id, Guid.NewGuid()));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Persons.DidNotReceive().UpdateAsync(Arg.Any<Person>(), Arg.Any<CancellationToken>());
    }

    // --- Family relationships are sensitive reads ---

    [Fact]
    public async Task GetFamilyRelationships_throws_without_family_read_grant()
    {
        var h = CreateHarness();
        h.DenyAll();

        var act = () => h.Sender.Send(new GetFamilyRelationshipsQuery(ActorId, Guid.NewGuid()));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
    }

    // --- Cross-entity ownership guards ---

    [Fact]
    public async Task UpdateActivity_throws_without_update_grant_on_resource()
    {
        var h = CreateHarness();
        h.DenyAll();
        var unit = Guid.NewGuid();
        var activity = Activity.Create(
            "Study Circle", null, null, null, unit, null, false, null,
            DateTimeRange.Create(Now, Now.AddHours(1)),
            ActivityVisibility.Members, ActivityStatus.Active, null);
        h.Repos.Activities.GetByIdAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);

        var act = () => h.Sender.Send(new UpdateActivityCommand(
            ActorId, activity.Id, "Renamed", null, null, null, null, false, null, "public"));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();

        // The evaluation must carry the owning organization unit in context.
        await h.Repos.Evaluator.Received(1)
            .EvaluateAsync(Arg.Is<AuthorizationRequest>(r =>
                r.Permission == CommunityPermissions.ActivityUpdate &&
                r.Context.OrganizationUnitId == unit &&
                r.Context.ResourceId == activity.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePersonProfile_throws_without_update_grant_on_resource()
    {
        var h = CreateHarness();
        h.DenyAll();
        var person = StubPerson();
        h.Repos.Persons.GetByIdAsync(person.Id, Arg.Any<CancellationToken>()).Returns(person);

        var act = () => h.Sender.Send(new UpdatePersonProfileCommand(
            ActorId, person.Id, "New", null, null, null));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();

        await h.Repos.Evaluator.Received(1)
            .EvaluateAsync(Arg.Is<AuthorizationRequest>(r =>
                r.Permission == CommunityPermissions.PersonUpdate &&
                r.Context.ResourceId == person.Id), Arg.Any<CancellationToken>());
    }

    // --- Household / membership / meeting guarded operations ---

    [Fact]
    public async Task AddHouseholdMember_throws_without_update_grant()
    {
        var h = CreateHarness();
        h.DenyAll();
        var household = Household.Create("Ridvan House", null);
        h.Repos.Households.GetByIdAsync(household.Id, Arg.Any<CancellationToken>()).Returns(household);

        var act = () => h.Sender.Send(new AddHouseholdMemberCommand(
            ActorId, household.Id, Guid.NewGuid(), "adult", Now));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
    }

    [Fact]
    public async Task ChangeMembershipStatus_throws_without_update_grant()
    {
        var h = CreateHarness();
        h.DenyAll();
        var membership = Membership.Create(Guid.NewGuid(), MembershipStatus.Active, EffectivePeriod.Create(Now));
        h.Repos.Memberships.GetByIdAsync(membership.Id, Arg.Any<CancellationToken>()).Returns(membership);

        var act = () => h.Sender.Send(new ChangeMembershipStatusCommand(
            ActorId, membership.Id, "suspended", Now));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
    }

    [Fact]
    public async Task RecordMeetingMinutes_throws_without_record_grant()
    {
        var h = CreateHarness();
        h.DenyAll();
        var meeting = Meeting.Create(
            "Feast", null, Now, Now.AddHours(1), "UTC", null,
            null, null, MeetingStatus.Scheduled, MeetingVisibility.Members);
        h.Repos.Meetings.GetByIdAsync(meeting.Id, Arg.Any<CancellationToken>()).Returns(meeting);

        var act = () => h.Sender.Send(new RecordMeetingMinutesCommand(ActorId, meeting.Id, "minutes"));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
    }
}
