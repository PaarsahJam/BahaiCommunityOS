using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Application.Permissions;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.Repositories;
using MediatR;

namespace CommunityOS.Community.Application.Queries;

public sealed record GetCommunityByIdQuery(Guid ActorId, Guid CommunityId) : IRequest<CommunityDto>;

internal sealed class GetCommunityByIdQueryHandler(
    ICommunityRepository communities,
    AuthorizationGuard guard)
    : IRequestHandler<GetCommunityByIdQuery, CommunityDto>
{
    public async Task<CommunityDto> Handle(GetCommunityByIdQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, CommunityPermissions.CommunityHierarchyRead,
            new AuthorizationContext(ResourceType: "community", ResourceId: query.CommunityId), ct);

        var community = await communities.GetByIdAsync(query.CommunityId, ct)
            ?? throw new CommunityNotFoundException(query.CommunityId);

        return community.ToDto();
    }
}
