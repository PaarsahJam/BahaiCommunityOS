using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Repositories;
using CommunityOS.Authorization.Infrastructure.Integration.Community;
using CommunityOS.Contracts.Community;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CommunityOS.Authorization.Tests.Integration;

public class PersonIdentityIntegrationEventConsumerTests
{
    private static readonly Guid AccountA = Guid.NewGuid();
    private static readonly Guid PersonA = Guid.NewGuid();

    private readonly IAuthorizationRelationshipRepository _relationships =
        Substitute.For<IAuthorizationRelationshipRepository>();

    private PersonIdentityIntegrationEventConsumer Consumer() =>
        new(_relationships, NullLogger<PersonIdentityIntegrationEventConsumer>.Instance);

    private void Seed(params AuthorizationRelationship[] tuples)
    {
        _relationships.ListAsync(
                Arg.Any<Guid?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AuthorizationRelationship>>(tuples));
    }

    private static AuthorizationRelationship CreateMemberTuple(Guid accountId, Guid personId) =>
        AuthorizationRelationship.Create(
            accountId,
            MemberPortalPermissionSet.Relation,
            MemberPortalPermissionSet.ObjectType,
            personId,
            MemberPortalPermissionSet.Permissions);

    [Fact]
    public void Consumer_implements_person_identity_integration_consumers()
    {
        typeof(PersonIdentityIntegrationEventConsumer)
            .Should().BeAssignableTo<IConsumer<PersonIdentityLinked>>();
        typeof(PersonIdentityIntegrationEventConsumer)
            .Should().BeAssignableTo<IConsumer<PersonIdentityUnlinked>>();
    }

    [Fact]
    public async Task Ensure_creates_exact_member_tuple_when_none_exist()
    {
        Seed();

        await Consumer().EnsureMemberPortalGrantsAsync(AccountA, PersonA, CancellationToken.None);

        await _relationships.Received(1).AddAsync(
            Arg.Is<AuthorizationRelationship>(r =>
                r.SubjectId == AccountA
                && r.Relation == MemberPortalPermissionSet.Relation
                && r.ObjectType == MemberPortalPermissionSet.ObjectType
                && r.ObjectId == PersonA
                && r.Permissions.SequenceEqual(MemberPortalPermissionSet.Permissions)),
            Arg.Any<CancellationToken>());
        await _relationships.DidNotReceiveWithAnyArgs().RemoveAsync(default, default);
    }

    [Fact]
    public async Task Ensure_is_idempotent_when_exact_tuple_exists()
    {
        Seed(CreateMemberTuple(AccountA, PersonA));

        await Consumer().EnsureMemberPortalGrantsAsync(AccountA, PersonA, CancellationToken.None);

        await _relationships.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _relationships.DidNotReceiveWithAnyArgs().RemoveAsync(default, default);
    }

    [Fact]
    public async Task Ensure_consolidates_stale_partial_member_tuples()
    {
        var partial = AuthorizationRelationship.Create(
            AccountA, MemberPortalPermissionSet.Relation,
            MemberPortalPermissionSet.ObjectType, PersonA, [MemberPortalPermissionSet.Permissions[0]]);
        var duplicate = CreateMemberTuple(AccountA, PersonA);
        Seed(partial, duplicate);

        await Consumer().EnsureMemberPortalGrantsAsync(AccountA, PersonA, CancellationToken.None);

        await _relationships.Received(2).RemoveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _relationships.Received(1).AddAsync(Arg.Any<AuthorizationRelationship>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ensure_preserves_admin_tuple_that_also_carries_member_permission()
    {
        var admin = AuthorizationRelationship.Create(
            AccountA, MemberPortalPermissionSet.Relation,
            MemberPortalPermissionSet.ObjectType, PersonA,
            ["community.person.read", "community.person.update"]);
        Seed(admin);

        await Consumer().EnsureMemberPortalGrantsAsync(AccountA, PersonA, CancellationToken.None);

        await _relationships.DidNotReceiveWithAnyArgs().RemoveAsync(default, default);
        await _relationships.Received(1).AddAsync(Arg.Any<AuthorizationRelationship>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ensure_queries_only_the_linked_person_resource()
    {
        Seed();

        await Consumer().EnsureMemberPortalGrantsAsync(AccountA, PersonA, CancellationToken.None);

        await _relationships.Received(1).ListAsync(
            AccountA,
            MemberPortalPermissionSet.Relation,
            MemberPortalPermissionSet.ObjectType,
            PersonA,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Revoke_removes_member_tuples_and_preserves_admin_tuples()
    {
        var member = CreateMemberTuple(AccountA, PersonA);
        var admin = AuthorizationRelationship.Create(
            AccountA, MemberPortalPermissionSet.Relation,
            MemberPortalPermissionSet.ObjectType, PersonA,
            ["community.person.read", "community.person.update"]);
        Seed(member, admin);

        await Consumer().RevokeMemberPortalGrantsAsync(AccountA, PersonA, CancellationToken.None);

        await _relationships.Received(1).RemoveAsync(member.Id, Arg.Any<CancellationToken>());
        await _relationships.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Revoke_is_idempotent_when_no_member_tuples_exist()
    {
        var admin = AuthorizationRelationship.Create(
            AccountA, MemberPortalPermissionSet.Relation,
            MemberPortalPermissionSet.ObjectType, PersonA, ["community.person.update"]);
        Seed(admin);

        await Consumer().RevokeMemberPortalGrantsAsync(AccountA, PersonA, CancellationToken.None);

        await _relationships.DidNotReceiveWithAnyArgs().RemoveAsync(default, default);
        await _relationships.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }
}