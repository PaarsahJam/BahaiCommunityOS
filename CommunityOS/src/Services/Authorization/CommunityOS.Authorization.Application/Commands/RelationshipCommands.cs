using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.DTOs;
using CommunityOS.Authorization.Application.Permissions;
using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Repositories;
using MediatR;

namespace CommunityOS.Authorization.Application.Commands;

public sealed record WriteRelationshipCommand(
    Guid ActorId,
    Guid SubjectId,
    string Relation,
    string ObjectType,
    Guid ObjectId,
    IReadOnlyList<string>? Permissions,
    Guid? OrganizationUnitId) : IRequest<RelationshipDto>;

internal sealed class WriteRelationshipCommandHandler(
    IAuthorizationRelationshipRepository relationships,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<WriteRelationshipCommand, RelationshipDto>
{
    public async Task<RelationshipDto> Handle(WriteRelationshipCommand request, CancellationToken ct)
    {
        await guard.RequireAsync(
            request.ActorId,
            PermissionCatalog.AuthzRelationshipWrite,
            new AuthorizationContext(OrganizationUnitId: request.OrganizationUnitId),
            ct);

        var relationship = AuthorizationRelationship.Create(
            request.SubjectId,
            request.Relation,
            request.ObjectType,
            request.ObjectId,
            request.Permissions);

        await relationships.AddAsync(relationship, ct);
        await DomainEventPublisher.PublishAsync(relationship, mediator, ct);

        return Map(relationship);
    }

    internal static RelationshipDto Map(AuthorizationRelationship relationship) => new(
        relationship.Id,
        relationship.SubjectId,
        relationship.Relation,
        relationship.ObjectType,
        relationship.ObjectId,
        relationship.Permissions);
}

public sealed record ReadRelationshipsQuery(
    Guid ActorId,
    Guid? SubjectId,
    string? Relation,
    string? ObjectType,
    Guid? ObjectId) : IRequest<IReadOnlyList<RelationshipDto>>;

internal sealed class ReadRelationshipsQueryHandler(
    IAuthorizationRelationshipRepository relationships,
    AuthorizationGuard guard) : IRequestHandler<ReadRelationshipsQuery, IReadOnlyList<RelationshipDto>>
{
    public async Task<IReadOnlyList<RelationshipDto>> Handle(ReadRelationshipsQuery request, CancellationToken ct)
    {
        // Reading another subject's relationships is a privileged operation.
        if (request.SubjectId is not null && request.SubjectId != request.ActorId)
            await guard.RequireAsync(request.ActorId, PermissionCatalog.AuthzRelationshipRead, null, ct);

        var tuples = await relationships.ListAsync(
            request.SubjectId, request.Relation, request.ObjectType, request.ObjectId, ct);

        return tuples.Select(WriteRelationshipCommandHandler.Map).ToList();
    }
}
