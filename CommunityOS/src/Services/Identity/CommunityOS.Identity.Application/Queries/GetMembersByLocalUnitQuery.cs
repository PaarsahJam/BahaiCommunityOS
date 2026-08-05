using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;

namespace CommunityOS.Identity.Application.Queries;

public sealed record GetMembersByLocalUnitQuery(Guid LocalUnitId) : IRequest<IReadOnlyList<MemberDto>>;

internal sealed class GetMembersByLocalUnitQueryHandler(IMemberRepository members)
    : IRequestHandler<GetMembersByLocalUnitQuery, IReadOnlyList<MemberDto>>
{
    public async Task<IReadOnlyList<MemberDto>> Handle(
        GetMembersByLocalUnitQuery query, CancellationToken ct)
    {
        var result = await members.GetByLocalUnitAsync(query.LocalUnitId, ct);
        return result.Select(m => m.ToDto()).ToList().AsReadOnly();
    }
}
