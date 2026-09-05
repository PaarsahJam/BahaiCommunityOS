using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Repositories;
using CommunityOS.Contracts.Community;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Authorization.Infrastructure.Integration.Community;

public static class MemberPortalPermissionSet
{
    public const string Relation = "has_permission";
    public const string ObjectType = "person";

    public static readonly IReadOnlyList<string> Permissions =
    [
        "community.person.read",
        "community.person.contact.read",
        "community.membership.read",
    ];

    public static bool IsMemberTuple(AuthorizationRelationship tuple) =>
        tuple.Permissions.Count > 0 && tuple.Permissions.All(Permissions.Contains);

    public static bool IsExactMemberSet(AuthorizationRelationship tuple) =>
        tuple.Permissions.Count == Permissions.Count && tuple.Permissions.All(Permissions.Contains);
}

public sealed class PersonIdentityIntegrationEventConsumer(
    IAuthorizationRelationshipRepository relationships,
    ILogger<PersonIdentityIntegrationEventConsumer> logger) :
    IConsumer<PersonIdentityLinked>,
    IConsumer<PersonIdentityUnlinked>
{
    public async Task Consume(ConsumeContext<PersonIdentityLinked> context)
    {
        await EnsureMemberPortalGrantsAsync(
            context.Message.IdentityAccountId, context.Message.PersonId, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<PersonIdentityUnlinked> context)
    {
        await RevokeMemberPortalGrantsAsync(
            context.Message.IdentityAccountId, context.Message.PersonId, context.CancellationToken);
    }

    public async Task EnsureMemberPortalGrantsAsync(
        Guid identityAccountId, Guid personId, CancellationToken ct)
    {
        var tuples = await relationships.ListAsync(
            subjectId: identityAccountId,
            relation: MemberPortalPermissionSet.Relation,
            objectType: MemberPortalPermissionSet.ObjectType,
            objectId: personId,
            ct);

        var memberTuples = tuples.Where(MemberPortalPermissionSet.IsMemberTuple).ToList();
        if (memberTuples.Count == 1 && MemberPortalPermissionSet.IsExactMemberSet(memberTuples[0]))
            return;

        foreach (var stale in memberTuples)
            await relationships.RemoveAsync(stale.Id, ct);

        await relationships.AddAsync(
            AuthorizationRelationship.Create(
                identityAccountId,
                MemberPortalPermissionSet.Relation,
                MemberPortalPermissionSet.ObjectType,
                personId,
                MemberPortalPermissionSet.Permissions),
            ct);

        logger.MemberPortalGrantsProvisioned(identityAccountId, personId);
    }

    public async Task RevokeMemberPortalGrantsAsync(
        Guid identityAccountId, Guid personId, CancellationToken ct)
    {
        var tuples = await relationships.ListAsync(
            subjectId: identityAccountId,
            relation: MemberPortalPermissionSet.Relation,
            objectType: MemberPortalPermissionSet.ObjectType,
            objectId: personId,
            ct);

        var memberTuples = tuples.Where(MemberPortalPermissionSet.IsMemberTuple).ToList();
        foreach (var member in memberTuples)
            await relationships.RemoveAsync(member.Id, ct);

        if (memberTuples.Count > 0)
            logger.MemberPortalGrantsRevoked(identityAccountId, personId);
    }
}

internal static partial class PersonIdentityIntegrationEventConsumerLogging
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Member portal grants provisioned for Identity account {IdentityAccountId} on person {PersonId}.")]
    public static partial void MemberPortalGrantsProvisioned(
        this ILogger logger, Guid identityAccountId, Guid personId);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Member portal grants revoked for Identity account {IdentityAccountId} on person {PersonId}.")]
    public static partial void MemberPortalGrantsRevoked(
        this ILogger logger, Guid identityAccountId, Guid personId);
}