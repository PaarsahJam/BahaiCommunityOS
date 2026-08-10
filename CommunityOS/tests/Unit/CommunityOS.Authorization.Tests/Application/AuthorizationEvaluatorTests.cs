using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Enumerations;
using CommunityOS.Authorization.Domain.Repositories;
using CommunityOS.Authorization.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CommunityOS.Authorization.Tests.Application;

public class AuthorizationEvaluatorTests
{
    private static readonly Guid OrgA = Guid.NewGuid();
    private static readonly Guid OrgB = Guid.NewGuid();
    private static readonly Guid RecordId = Guid.NewGuid();
    private static readonly Guid Subject = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    private const string ReadPermission = "records.record.read";
    private const string AdminPermission = "authz.role.assign";

    private readonly IRoleAssignmentRepository _assignments = Substitute.For<IRoleAssignmentRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IDelegationRepository _delegations = Substitute.For<IDelegationRepository>();
    private readonly IBreakGlassRequestRepository _breakGlass = Substitute.For<IBreakGlassRequestRepository>();
    private readonly IAuthorizationRelationshipRepository _relationships = Substitute.For<IAuthorizationRelationshipRepository>();
    private readonly IOrganizationContextProvider _orgContext = Substitute.For<IOrganizationContextProvider>();

    private AuthorizationEvaluator Evaluator() =>
        new(_assignments, _roles, _delegations, _breakGlass, _relationships, _orgContext, NullLogger<AuthorizationEvaluator>.Instance);

    private void EnsureDefaults()
    {
        _assignments.ListBySubjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        _roles.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Role?)null);
        _delegations.ListByDelegateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        _breakGlass.ListByRequesterAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        _relationships.ListBySubjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private void NoGrants() => GrantRole("global-reader", null, []);

    private void GrantRole(
        string roleCode,
        Guid? orgId,
        IReadOnlyList<string> permissions,
        bool enabled = true,
        bool revoked = false,
        Guid? resourceId = null)
    {
        EnsureDefaults();

        var role = Role.Create(roleCode, roleCode, null, permissions);
        if (!enabled) role.Disable();
        _roles.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);

        var scope = orgId is null
            ? AuthorizationScope.Global()
            : AuthorizationScope.Scoped(ScopeType.Local, orgId.Value);
        var assignment = RoleAssignment.Create(Subject, role.Id, roleCode, scope, Actor, DateTime.UtcNow.AddHours(-1), null, null, null);
        if (revoked) assignment.Revoke(Actor, "test");

        _assignments.ListBySubjectAsync(Subject, Arg.Any<CancellationToken>()).Returns([assignment]);
    }

    private void GrantDelegation(Guid delegateId, IReadOnlyList<string> permissions, Guid orgId, bool expired = false)
    {
        EnsureDefaults();

        var now = DateTime.UtcNow;
        var delegation = Delegation.Create(
            Actor, delegateId, permissions, AuthorizationScope.Scoped(ScopeType.Local, orgId),
            expired ? now.AddHours(-2) : now.AddHours(-1),
            expired ? now.AddHours(-1) : now.AddHours(1), "coverage");
        _delegations.ListByDelegateAsync(delegateId, Arg.Any<CancellationToken>()).Returns([delegation]);
    }

    private void GrantBreakGlass(Guid requesterId, IReadOnlyList<string> permissions, Guid orgId, bool expired = false)
    {
        EnsureDefaults();

        var request = BreakGlassRequest.Create(requesterId, AuthorizationScope.Scoped(ScopeType.Local, orgId), permissions, "emergency", TimeSpan.FromMinutes(30));
        var now = DateTime.UtcNow;
        request.Approve(Actor, expired ? now.AddHours(-2) : now.AddHours(-1), expired ? TimeSpan.FromMinutes(30) : TimeSpan.FromMinutes(90));
        _breakGlass.ListByRequesterAsync(requesterId, Arg.Any<CancellationToken>()).Returns([request]);
    }

    private void GrantRelationship(Guid subjectId, Guid objectId, IReadOnlyList<string> permissions, string relation = "has_permission")
    {
        EnsureDefaults();

        var relationship = AuthorizationRelationship.Create(subjectId, relation, "record", objectId, permissions);
        _relationships.ListBySubjectAsync(subjectId, Arg.Any<CancellationToken>()).Returns([relationship]);
    }

    private async Task<AuthorizationDecision> EvaluateAsync(
        Guid subjectId, string permission, AuthorizationContext context) =>
        await Evaluator().EvaluateAsync(new AuthorizationRequest(subjectId, permission, context));

    [Fact]
    public async Task Denies_by_default_when_subject_has_no_grants()
    {
        EnsureDefaults();

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(OrgA));

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(AuthorizationDecisionReason.DeniedByDefault);
    }

    [Fact]
    public async Task Denies_missing_subject()
    {
        var decision = await EvaluateAsync(Guid.Empty, ReadPermission, new AuthorizationContext(OrgA));

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(AuthorizationDecisionReason.MissingSubject);
    }

    [Fact]
    public async Task Denies_invalid_permission()
    {
        NoGrants();

        var decision = await EvaluateAsync(Subject, "no-dash!", new AuthorizationContext(OrgA));

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(AuthorizationDecisionReason.InvalidPermission);
    }

    [Fact]
    public async Task Allows_role_assignment_within_exact_org_scope()
    {
        GrantRole("editor", OrgA, [ReadPermission]);

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(OrgA));

        decision.Allowed.Should().BeTrue();
        decision.PolicyReferences.Should().ContainSingle(r => r.StartsWith("role:editor:"));
    }

    [Fact]
    public async Task Denies_role_assignment_outside_org_scope()
    {
        GrantRole("editor", OrgA, [ReadPermission]);

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(OrgB));

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(AuthorizationDecisionReason.ScopeMismatch);
    }

    [Fact]
    public async Task Denies_when_role_does_not_grant_permission()
    {
        GrantRole("editor", OrgA, ["records.record.update"]);

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(OrgA));

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(AuthorizationDecisionReason.NoPermission);
    }

    [Fact]
    public async Task Denies_when_role_is_disabled()
    {
        GrantRole("editor", OrgA, [ReadPermission], enabled: false);

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(OrgA));

        decision.Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Denies_when_assignment_is_revoked()
    {
        GrantRole("editor", OrgA, [ReadPermission], revoked: true);

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(OrgA));

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(AuthorizationDecisionReason.DeniedByDefault);
    }

    [Fact]
    public async Task Denies_when_role_reference_is_dangling()
    {
        EnsureDefaults();

        var role = Role.Create("editor", "Editor", null, [ReadPermission]);
        var assignment = RoleAssignment.Create(Subject, role.Id, role.Code, AuthorizationScope.Scoped(ScopeType.Local, OrgA), Actor, DateTime.UtcNow.AddHours(-1), null, null, null);
        _assignments.ListBySubjectAsync(Subject, Arg.Any<CancellationToken>()).Returns([assignment]);
        _roles.GetByIdAsync(assignment.RoleId, Arg.Any<CancellationToken>()).Returns((Role?)null);

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(OrgA));

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(AuthorizationDecisionReason.NoPermission);
    }

    [Fact]
    public async Task Global_data_permission_does_not_grant_resource_access()
    {
        GrantRole("global-reader", null, [ReadPermission]);

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(OrgA, "record", RecordId));

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(AuthorizationDecisionReason.ScopeMismatch);
    }

    [Fact]
    public async Task Global_data_permission_applies_to_global_checks()
    {
        GrantRole("global-reader", null, [ReadPermission]);

        var decision = await EvaluateAsync(Subject, ReadPermission, AuthorizationContext.Empty);

        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Global_admin_permission_applies_at_org_scope()
    {
        GrantRole("global-admin", null, [AdminPermission]);

        var decision = await EvaluateAsync(Subject, AdminPermission, new AuthorizationContext(OrgA));

        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Global_admin_permission_does_not_grant_data_permission_at_resource()
    {
        GrantRole("global-admin", null, [AdminPermission]);

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(OrgA, "record", RecordId));

        decision.Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Allows_delegation_within_org_scope()
    {
        GrantDelegation(Subject, [ReadPermission], OrgA);

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(OrgA));

        decision.Allowed.Should().BeTrue();
        decision.PolicyReferences.Should().ContainSingle(r => r.StartsWith("delegation:"));
    }

    [Fact]
    public async Task Denies_expired_delegation()
    {
        GrantDelegation(Subject, [ReadPermission], OrgA, expired: true);

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(OrgA));

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(AuthorizationDecisionReason.DeniedByDefault);
    }

    [Fact]
    public async Task Denies_delegation_outside_org_scope()
    {
        GrantDelegation(Subject, [ReadPermission], OrgA);

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(OrgB));

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(AuthorizationDecisionReason.ScopeMismatch);
    }

    [Fact]
    public async Task Allows_approved_break_glass_within_window()
    {
        GrantBreakGlass(Subject, [ReadPermission], OrgA);

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(OrgA));

        decision.Allowed.Should().BeTrue();
        decision.PolicyReferences.Should().ContainSingle(r => r.StartsWith("breakglass:"));
    }

    [Fact]
    public async Task Denies_expired_break_glass()
    {
        GrantBreakGlass(Subject, [ReadPermission], OrgA, expired: true);

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(OrgA));

        decision.Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Allows_relationship_grant_on_exact_resource()
    {
        GrantRelationship(Subject, RecordId, [ReadPermission]);

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(OrgA, "record", RecordId));

        decision.Allowed.Should().BeTrue();
        decision.PolicyReferences.Should().ContainSingle(r => r.StartsWith("relationship:"));
    }

    [Fact]
    public async Task Denies_relationship_grant_on_different_resource()
    {
        GrantRelationship(Subject, RecordId, [ReadPermission]);

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(OrgA, "record", Guid.NewGuid()));

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(AuthorizationDecisionReason.ScopeMismatch);
    }

    [Fact]
    public async Task Denies_relationship_without_requested_permission()
    {
        GrantRelationship(Subject, RecordId, ["records.record.update"]);

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(OrgA, "record", RecordId));

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(AuthorizationDecisionReason.NoPermission);
    }

    [Fact]
    public async Task Allows_when_org_unit_is_descendant_via_organization_provider()
    {
        var localCommittee = Guid.NewGuid();
        GrantRole("editor", OrgA, [ReadPermission]);
        _orgContext.IsAncestorOrSelfAsync(OrgA, localCommittee, Arg.Any<CancellationToken>()).Returns(true);

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(localCommittee));

        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Denies_when_organization_provider_reports_no_hierarchy()
    {
        var child = Guid.NewGuid();
        GrantRole("editor", OrgA, [ReadPermission]);
        _orgContext.IsAncestorOrSelfAsync(OrgA, child, Arg.Any<CancellationToken>()).Returns(false);

        var decision = await EvaluateAsync(Subject, ReadPermission, new AuthorizationContext(child));

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(AuthorizationDecisionReason.ScopeMismatch);
    }

    [Fact]
    public async Task Batch_evaluates_each_request_independently()
    {
        GrantRole("editor", OrgA, [ReadPermission]);
        var evaluator = Evaluator();

        var requests = new[]
        {
            new AuthorizationRequest(Subject, ReadPermission, new AuthorizationContext(OrgA)),
            new AuthorizationRequest(Subject, ReadPermission, new AuthorizationContext(OrgB)),
        };

        var decisions = await evaluator.EvaluateBatchAsync(requests);

        decisions.Should().HaveCount(2);
        decisions[0].Allowed.Should().BeTrue();
        decisions[1].Allowed.Should().BeFalse();
    }
}
