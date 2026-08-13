using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Application.Permissions;
using CommunityOS.Community.Domain.Repositories;
using MediatR;

namespace CommunityOS.Community.Application.Queries;

public sealed record GetCommunitiesByParentQuery(
    Guid ActorId,
    Guid ParentId) : IRequest<IReadOnlyList<CommunityDto>>;

internal sealed class GetCommunitiesByParentQueryHandler(
    ICommunityRepository communities,
    AuthorizationGuard guard)
    : IRequestHandler<GetCommunitiesByParentQuery, IReadOnlyList<CommunityDto>>
{
    public async Task<IReadOnlyList<CommunityDto>> Handle(
        GetCommunitiesByParentQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, CommunityPermissions.CommunityHierarchyRead,
            new AuthorizationContext(ResourceType: "community", ResourceId: query.ParentId), ct);

        var result = await communities.GetByParentAsync(query.ParentId, ct);
        return result.Select(c => c.ToDto()).ToList().AsReadOnly();
    }
}
