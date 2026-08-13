using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Application.Permissions;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.Repositories;
using MediatR;

namespace CommunityOS.Community.Application.Queries;

public sealed record GetMembershipByIdQuery(
    Guid ActorId,
    Guid MembershipId) : IRequest<MembershipDto>;

internal sealed class GetMembershipByIdQueryHandler(
    IMembershipRepository memberships,
    AuthorizationGuard guard) : IRequestHandler<GetMembershipByIdQuery, MembershipDto>
{
    public async Task<MembershipDto> Handle(GetMembershipByIdQuery request, CancellationToken ct)
    {
        var membership = await memberships.GetByIdAsync(request.MembershipId, ct)
            ?? throw new MembershipNotFoundException(request.MembershipId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.MembershipRead,
            new AuthorizationContext(ResourceType: "membership", ResourceId: request.MembershipId), ct);

        return membership.ToDto();
    }
}

public sealed record GetMembershipByPersonQuery(
    Guid ActorId,
    Guid PersonId) : IRequest<MembershipDto?>;

internal sealed class GetMembershipByPersonQueryHandler(
    IMembershipRepository memberships,
    AuthorizationGuard guard) : IRequestHandler<GetMembershipByPersonQuery, MembershipDto?>
{
    public async Task<MembershipDto?> Handle(GetMembershipByPersonQuery request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, CommunityPermissions.MembershipRead,
            new AuthorizationContext(ResourceType: "person", ResourceId: request.PersonId), ct);

        var membership = await memberships.GetByPersonAsync(request.PersonId, ct);
        return membership?.ToDto();
    }
}

public sealed record GetMembershipsQuery(
    Guid ActorId) : IRequest<IReadOnlyList<MembershipDto>>;

internal sealed class GetMembershipsQueryHandler(
    IMembershipRepository memberships,
    AuthorizationGuard guard) : IRequestHandler<GetMembershipsQuery, IReadOnlyList<MembershipDto>>
{
    public async Task<IReadOnlyList<MembershipDto>> Handle(GetMembershipsQuery request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, CommunityPermissions.MembershipRead, null, ct);

        var all = await memberships.ListAsync(ct);
        return all.Select(m => m.ToDto()).ToList();
    }
}
