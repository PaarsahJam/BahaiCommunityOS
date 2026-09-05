using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Enumerations;
using CommunityOS.Authorization.Domain.Repositories;
using CommunityOS.Authorization.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CommunityOS.Authorization.Tests.Application;

public class MemberPortalAuthorizationTests
{
    private static readonly Guid AccountA = Guid.NewGuid();
    private static readonly Guid AccountB = Guid.NewGuid();
    private static readonly Guid PersonA = Guid.NewGuid();
    private static readonly Guid PersonB = Guid.NewGuid();

    private const string PersonRead = "community.person.read";
    private const string PersonContactRead = "community.person.contact.read";
    private const string MembershipRead = "community.membership.read";

    private static readonly string[] MemberPermissions = [PersonRead, PersonContactRead, MembershipRead];

    private readonly IRoleAssignmentRepository _assignments = Substitute.For<IRoleAssignmentRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IDelegationRepository _delegations = Substitute.For<IDelegationRepository>();
    private readonly IBreakGlassRequestRepository _breakGlass = Substitute.For<IBreakGlassRequestRepository>();
    private readonly IAuthorizationRelationshipRepository _relationships = Substitute.For<IAuthorizationRelationshipRepository>();
    private readonly IOrganizationContextProvider _orgContext = Substitute.For<IOrganizationContextProvider>();

    private AuthorizationEvaluator Evaluator() =>
        new(_assignments, _roles, _delegations, _breakGlass, _relationships, _orgContext, NullLogger<AuthorizationEvaluator>.Instance);

    private void Defaults()
    {
        _assignments.ListBySubjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        _roles.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Role?)null);
        _delegations.ListByDelegateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        _breakGlass.ListByRequesterAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        _relationships.ListBySubjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private void GrantPersonAccess(Guid accountId, Guid personId, IReadOnlyList<string> permissions)
    {
        Defaults();

        var relationship = AuthorizationRelationship.Create(
            accountId, "has_permission", "person", personId, permissions);
        _relationships.ListBySubjectAsync(accountId, Arg.Any<CancellationToken>())
            .Returns([relationship]);
    }

    private void GrantMemberRole(Guid accountId)
    {
        Defaults();

        var role = Role.Create("member", "Member", null, []);
        _roles.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);
        var assignment = RoleAssignment.Create(
            accountId, role.Id, role.Code, AuthorizationScope.Global(), Guid.NewGuid(),
            DateTime.UtcNow.AddHours(-1), null, null, null);
        _assignments.ListBySubjectAsync(accountId, Arg.Any<CancellationToken>())
            .Returns([assignment]);
    }

    private async Task<AuthorizationDecision> EvaluateAsync(Guid accountId, string permission, Guid personId) =>
        await Evaluator().EvaluateAsync(new AuthorizationRequest(
            accountId,
            permission,
            new AuthorizationContext(ResourceType: "person", ResourceId: personId)));

    [Fact]
    public async Task Member_reads_own_person_resource()
    {
        GrantPersonAccess(AccountA, PersonA, MemberPermissions);

        var decision = await EvaluateAsync(AccountA, PersonRead, PersonA);

        decision.Allowed.Should().BeTrue();
        decision.PolicyReferences.Should().ContainSingle(r => r.StartsWith("relationship:"));
    }

    [Fact]
    public async Task Member_cannot_read_another_person_profile()
    {
        GrantPersonAccess(AccountA, PersonA, MemberPermissions);

        var decision = await EvaluateAsync(AccountA, PersonRead, PersonB);

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(AuthorizationDecisionReason.ScopeMismatch);
    }

    [Fact]
    public async Task Member_cannot_read_another_person_contact_details()
    {
        GrantPersonAccess(AccountA, PersonA, MemberPermissions);

        var decision = await EvaluateAsync(AccountA, PersonContactRead, PersonB);

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(AuthorizationDecisionReason.ScopeMismatch);
    }

    [Fact]
    public async Task Member_cannot_read_another_person_membership()
    {
        GrantPersonAccess(AccountA, PersonA, MemberPermissions);

        var decision = await EvaluateAsync(AccountA, MembershipRead, PersonB);

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(AuthorizationDecisionReason.ScopeMismatch);
    }

    [Fact]
    public async Task Each_person_grant_is_isolated_to_its_subject()
    {
        GrantPersonAccess(AccountB, PersonB, MemberPermissions);

        (await EvaluateAsync(AccountB, PersonRead, PersonB)).Allowed.Should().BeTrue();
        (await EvaluateAsync(AccountB, PersonRead, PersonA)).Allowed.Should().BeFalse();
        (await EvaluateAsync(AccountA, PersonRead, PersonA)).Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Person_grant_does_not_leak_to_other_resource_types()
    {
        GrantPersonAccess(AccountA, PersonA, MemberPermissions);

        var decision = await Evaluator().EvaluateAsync(new AuthorizationRequest(
            AccountA,
            PersonRead,
            new AuthorizationContext(ResourceType: "record", ResourceId: PersonA)));

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(AuthorizationDecisionReason.ScopeMismatch);
    }

    [Fact]
    public async Task Member_role_without_person_permissions_grants_nothing_per_person()
    {
        GrantMemberRole(AccountA);

        var decision = await EvaluateAsync(AccountA, PersonRead, PersonA);

        decision.Allowed.Should().BeFalse();
    }
}