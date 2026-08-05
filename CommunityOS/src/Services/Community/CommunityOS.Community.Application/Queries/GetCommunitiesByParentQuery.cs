using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Domain.Repositories;
using MediatR;

namespace CommunityOS.Community.Application.Queries;

public sealed record GetCommunitiesByParentQuery(Guid ParentId) : IRequest<IReadOnlyList<CommunityDto>>;

internal sealed class GetCommunitiesByParentQueryHandler(ICommunityRepository communities)
    : IRequestHandler<GetCommunitiesByParentQuery, IReadOnlyList<CommunityDto>>
{
    public async Task<IReadOnlyList<CommunityDto>> Handle(
        GetCommunitiesByParentQuery query, CancellationToken ct)
    {
        var result = await communities.GetByParentAsync(query.ParentId, ct);
        return result.Select(c => c.ToDto()).ToList().AsReadOnly();
    }
}
