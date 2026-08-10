using CommunityOS.Authorization.Application;
using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Commands;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Application.Permissions;
using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Enumerations;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Authorization.Domain.Repositories;
using CommunityOS.Authorization.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CommunityOS.Authorization.Tests.Application;

/// <summary>
/// Privilege-escalation regression tests. These drive the real command
/// handlers through the MediatR pipeline (validation + guard + evaluator) with
/// substitute persistence, verifying that forged, scoped or stale grants are
/// always denied.
/// </summary>
public class SecurityTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid Victim = Guid.NewGuid();
    private static readonly Guid OrgA = Guid.NewGuid();
    private static readonly Guid OrgB = Guid.NewGuid();

    private const string ReadPermission = "records.record.read";

    private sealed record RepoSet(
        IRoleAssignmentRepository Assignments,
        IRoleRepository Roles,
        IDelegationRepository Delegations,
        IBreakGlassRequestRepository BreakGlass,
        IAuthorizationRelationshipRepository Relationships,
        IOrganizationContextProvider OrgContext)
    {
        public static RepoSet Create()
        {
            var set = new RepoSet(
                Substitute.For<IRoleAssignmentRepository>(),
                Substitute.For<IRoleRepository>(),
                Substitute.For<IDelegationRepository>(),
                Substitute.For<IBreakGlassRequestRepository>(),
                Substitute.For<IAuthorizationRelationshipRepository>(),
                Substitute.For<IOrganizationContextProvider>());
            set.Assignments.ListBySubjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
            set.Roles.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Role?)null);
            set.Delegations.ListByDelegateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
            set.BreakGlass.ListByRequesterAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
            set.Relationships.ListBySubjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
            set.OrgContext.IsAncestorOrSelfAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
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
            services.Configure<AuthorizationOptions>(_ => { });
            services.AddAuthorizationApplication();

            services.AddScoped(_ => Repos.Assignments);
            services.AddScoped(_ => Repos.Roles);
            services.AddScoped(_ => Repos.Delegations);
            services.AddScoped(_ => Repos.BreakGlass);
            services.AddScoped(_ => Repos.Relationships);
            services.AddScoped(_ => Repos.OrgContext);

            Provider = services.BuildServiceProvider();
        }

        public ISender Sender => Provider.GetRequiredService<ISender>();

        public void GrantGlobalRole(string roleCode, params string[] permissions)
        {
            var role = Role.Create(roleCode, roleCode, null, permissions);
            Repos.Roles.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);
            Repos.Assignments.ListBySubjectAsync(Actor, Arg.Any<CancellationToken>())
                .Returns([
                    RoleAssignment.Create(Actor, role.Id, role.Code, AuthorizationScope.Global(), Actor, DateTime.UtcNow.AddHours(-1), null, null, null)
                ]);
        }

        public void GrantLocalRole(string roleCode, params string[] permissions) =>
            GrantScopedRole(roleCode, ScopeType.Local, OrgA, permissions);

        public void GrantScopedRole(string roleCode, ScopeType scopeType, Guid scopeId, params string[] permissions)
        {
            var role = Role.Create(roleCode, roleCode, null, permissions);
            Repos.Roles.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);
            Repos.Assignments.ListBySubjectAsync(Actor, Arg.Any<CancellationToken>())
                .Returns([
                    RoleAssignment.Create(Actor, role.Id, role.Code, AuthorizationScope.Scoped(scopeType, scopeId), Actor, DateTime.UtcNow.AddHours(-1), null, null, null)
                ]);
        }
    }

    private static Harness CreateHarness() => new();

    [Fact]
    public async Task Proxy_check_for_another_subject_requires_authz_check()
    {
        var h = CreateHarness();
        h.GrantLocalRole("local-admin", PermissionCatalog.AuthzRoleAssign);

        var act = () => h.Sender.Send(new CheckPermissionCommand(
            ActorId: Actor, SubjectId: Victim, Permission: ReadPermission,
            OrganizationUnitId: OrgA, ResourceType: null, ResourceId: null, CheckKey: "k"));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
    }

    [Fact]
    public async Task Proxy_check_is_allowed_when_actor_holds_authz_check()
    {
        var h = CreateHarness();
        h.GrantGlobalRole("auditor", PermissionCatalog.AuthzCheck);

        var editorRole = Role.Create("editor", "Editor", null, [ReadPermission]);
        h.Repos.Roles.GetByIdAsync(editorRole.Id, Arg.Any<CancellationToken>()).Returns(editorRole);
        h.Repos.Assignments.ListBySubjectAsync(Victim, Arg.Any<CancellationToken>())
            .Returns([
                RoleAssignment.Create(Victim, editorRole.Id, editorRole.Code,
                    AuthorizationScope.Scoped(ScopeType.Local, OrgA), Actor, DateTime.UtcNow.AddHours(-1), null, null, null)
            ]);

        var result = await h.Sender.Send(new CheckPermissionCommand(
            ActorId: Actor, SubjectId: Victim, Permission: ReadPermission,
            OrganizationUnitId: OrgA, ResourceType: null, ResourceId: null, CheckKey: "k"));

        result.Allowed.Should().BeTrue();
        result.CheckKey.Should().Be("k");
    }

    [Fact]
    public async Task Self_check_does_not_require_authz_check()
    {
        var h = CreateHarness();
        h.GrantLocalRole("editor", ReadPermission);

        var result = await h.Sender.Send(new CheckPermissionCommand(
            ActorId: Actor, SubjectId: Actor, Permission: ReadPermission,
            OrganizationUnitId: OrgA, ResourceType: null, ResourceId: null, CheckKey: "self"));

        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Delegation_cannot_grant_permissions_the_delegator_does_not_hold()
    {
        var h = CreateHarness();
        h.GrantGlobalRole("global-admin", PermissionCatalog.AuthzDelegationGrant);

        var act = () => h.Sender.Send(new GrantDelegationCommand(
            ActorId: Actor, DelegateId: Victim, Permissions: [ReadPermission],
            ScopeType: ScopeType.Local.Name, ScopeId: OrgA, ResourceType: null,
            StartsOn: DateTime.UtcNow, ExpiresOn: DateTime.UtcNow.AddDays(1), Reason: "cover"));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
    }

    [Fact]
    public async Task Delegation_cannot_escape_the_delegators_scope()
    {
        var h = CreateHarness();
        h.GrantLocalRole("editor", ReadPermission, PermissionCatalog.AuthzDelegationGrant);

        // Actor holds records.record.read only at OrgA but attempts to delegate
        // it at OrgB.
        var act = () => h.Sender.Send(new GrantDelegationCommand(
            ActorId: Actor, DelegateId: Victim, Permissions: [ReadPermission],
            ScopeType: ScopeType.Local.Name, ScopeId: OrgB, ResourceType: null,
            StartsOn: DateTime.UtcNow, ExpiresOn: DateTime.UtcNow.AddDays(1), Reason: "cover"));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
    }

    [Fact]
    public async Task Delegation_within_held_permissions_succeeds()
    {
        var h = CreateHarness();
        h.GrantLocalRole("editor", ReadPermission, PermissionCatalog.AuthzDelegationGrant);

        var result = await h.Sender.Send(new GrantDelegationCommand(
            ActorId: Actor, DelegateId: Victim, Permissions: [ReadPermission],
            ScopeType: ScopeType.Local.Name, ScopeId: OrgA, ResourceType: null,
            StartsOn: DateTime.UtcNow, ExpiresOn: DateTime.UtcNow.AddDays(1), Reason: "cover"));

        result.DelegateId.Should().Be(Victim);
        result.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task Delegation_exceeding_duration_limit_is_rejected()
    {
        var h = CreateHarness();
        h.GrantLocalRole("editor", ReadPermission, PermissionCatalog.AuthzDelegationGrant);

        var act = () => h.Sender.Send(new GrantDelegationCommand(
            ActorId: Actor, DelegateId: Victim, Permissions: [ReadPermission],
            ScopeType: ScopeType.Local.Name, ScopeId: OrgA, ResourceType: null,
            StartsOn: DateTime.UtcNow, ExpiresOn: DateTime.UtcNow.AddDays(31), Reason: "cover"));

        await act.Should().ThrowAsync<InvalidDelegationRequestException>();
    }

    [Fact]
    public async Task Revoking_another_subjects_delegation_requires_authz_delegation_revoke()
    {
        var h = CreateHarness();
        var delegation = Delegation.Create(
            Victim, Actor, [ReadPermission], AuthorizationScope.Scoped(ScopeType.Local, OrgA),
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(1), null);
        h.Repos.Delegations.GetByIdAsync(delegation.Id, Arg.Any<CancellationToken>()).Returns(delegation);

        var act = () => h.Sender.Send(new RevokeDelegationCommand(ActorId: Actor, DelegationId: delegation.Id, Reason: null));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
    }

    [Fact]
    public async Task Delegator_can_always_revoke_own_delegation_without_capability()
    {
        var h = CreateHarness();
        var delegation = Delegation.Create(
            Actor, Victim, [ReadPermission], AuthorizationScope.Scoped(ScopeType.Local, OrgA),
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(1), null);
        h.Repos.Delegations.GetByIdAsync(delegation.Id, Arg.Any<CancellationToken>()).Returns(delegation);

        var result = await h.Sender.Send(new RevokeDelegationCommand(ActorId: Actor, DelegationId: delegation.Id, Reason: "done"));

        result.IsRevoked.Should().BeTrue();
    }
}
