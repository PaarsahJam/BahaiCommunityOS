using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;

namespace CommunityOS.Identity.Application.Queries;

public sealed record GetMemberByIdQuery(Guid MemberId) : IRequest<MemberDto>;

internal sealed class GetMemberByIdQueryHandler(IMemberRepository members)
    : IRequestHandler<GetMemberByIdQuery, MemberDto>
{
    public async Task<MemberDto> Handle(GetMemberByIdQuery query, CancellationToken ct)
    {
        var member = await members.GetByIdAsync(query.MemberId, ct)
            ?? throw new MemberNotFoundException(query.MemberId);

        return member.ToDto();
    }
}
