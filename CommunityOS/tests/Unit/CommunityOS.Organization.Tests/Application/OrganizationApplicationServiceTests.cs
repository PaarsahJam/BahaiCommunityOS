using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Organization.Application;
using CommunityOS.Organization.Application.Commands;
using CommunityOS.Organization.Application.Permissions;
using CommunityOS.Organization.Application.Queries;
using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Enumerations;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.Repositories;
using CommunityOS.Organization.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CommunityOS.Organization.Tests.Application;

using Organization = CommunityOS.Organization.Domain.Aggregates.Organization;

public class OrganizationApplicationServiceTests
{
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid OrganizationId = Guid.NewGuid();

    private sealed record RepoSet(
        IOrganizationRepository Organizations,
        IOrganizationUnitRepository OrganizationUnits,
        IAppointmentRepository Appointments,
        ICommitteeRepository Committees,
        IInstitutionRepository Institutions,
        IDelegationFactRepository DelegationFacts,
        IAuthorizationEvaluator Evaluator)
    {
        public static RepoSet Create()
        {
            var set = new RepoSet(
                Substitute.For<IOrganizationRepository>(),
                Substitute.For<IOrganizationUnitRepository>(),
                Substitute.For<IAppointmentRepository>(),
                Substitute.For<ICommitteeRepository>(),
                Substitute.For<IInstitutionRepository>(),
                Substitute.For<IDelegationFactRepository>(),
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
            services.AddOrganizationApplication();

            services.AddScoped(_ => Repos.Organizations);
            services.AddScoped(_ => Repos.OrganizationUnits);
            services.AddScoped(_ => Repos.Appointments);
            services.AddScoped(_ => Repos.Committees);
            services.AddScoped(_ => Repos.Institutions);
            services.AddScoped(_ => Repos.DelegationFacts);
            services.AddScoped(_ => Repos.Evaluator);
            services.AddScoped<AuthorizationGuard>();

            Provider = services.BuildServiceProvider();
        }

        public ISender Sender => Provider.GetRequiredService<ISender>();
    }

    private static Harness CreateHarness() => new();

    [Fact]
    public async Task CreateOrganization_persists_and_returns_dto()
    {
        var h = CreateHarness();

        var result = await h.Sender.Send(new CreateOrganizationCommand(
            ActorId: ActorId,
            Name: "Ridvan Cluster",
            OrganizationType: "LocalSpiritualAssembly",
            JurisdictionType: "Local",
            JurisdictionScopeId: Guid.NewGuid(),
            EstablishedOn: null));

        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("Ridvan Cluster");
        await h.Repos.Organizations.Received(1)
            .AddAsync(Arg.Any<Organization>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrganization_rejects_duplicate_name()
    {
        var h = CreateHarness();
        h.Repos.Organizations.GetByNameAsync("Ridvan Cluster", Arg.Any<CancellationToken>())
            .Returns(Organization.Create(
                "Ridvan Cluster", "LocalSpiritualAssembly",
                Jurisdiction.Create(
                    JurisdictionType.Local, Guid.NewGuid())));

        var act = () => h.Sender.Send(new CreateOrganizationCommand(
            ActorId: ActorId,
            Name: "Ridvan Cluster",
            OrganizationType: "LocalSpiritualAssembly",
            JurisdictionType: "Local",
            JurisdictionScopeId: Guid.NewGuid(),
            EstablishedOn: null));

        await act.Should().ThrowAsync<OrganizationNameAlreadyExistsException>();
    }

    [Fact]
    public async Task GetOrganization_throws_when_absent()
    {
        var h = CreateHarness();

        var act = () => h.Sender.Send(new GetOrganizationByIdQuery(ActorId, OrganizationId));

        await act.Should().ThrowAsync<OrganizationNotFoundException>();
    }

    [Fact]
    public async Task GetOrganization_requires_read_permission()
    {
        var h = CreateHarness();
        var org = Organization.Create(
            "Ridvan Cluster", "LocalSpiritualAssembly",
            Jurisdiction.Create(
                JurisdictionType.Local, Guid.NewGuid()));
        h.Repos.Organizations.GetByIdAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);
        h.Repos.Evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<AuthorizationRequest>();
                return AuthorizationDecision.Allow("test-decision", [request.Permission], DateTime.UtcNow);
            });

        var result = await h.Sender.Send(new GetOrganizationByIdQuery(ActorId, org.Id));

        result.Id.Should().Be(org.Id);
        await h.Repos.Evaluator.Received(1)
            .EvaluateAsync(Arg.Is<AuthorizationRequest>(r =>
                r.SubjectId == ActorId &&
                r.Permission == OrganizationPermissions.OrgRead), Arg.Any<CancellationToken>());
    }
}
