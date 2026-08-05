using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.Repositories;
using MediatR;

namespace CommunityOS.Community.Application.Queries;

public sealed record GetCommunityByIdQuery(Guid CommunityId) : IRequest<CommunityDto>;

internal sealed class GetCommunityByIdQueryHandler(ICommunityRepository communities)
    : IRequestHandler<GetCommunityByIdQuery, CommunityDto>
{
    public async Task<CommunityDto> Handle(GetCommunityByIdQuery query, CancellationToken ct)
    {
        var community = await communities.GetByIdAsync(query.CommunityId, ct)
            ?? throw new CommunityNotFoundException(query.CommunityId);

        return community.ToDto();
    }
}
