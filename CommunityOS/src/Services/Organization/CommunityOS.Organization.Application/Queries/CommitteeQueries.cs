using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Organization.Application.Commands;
using CommunityOS.Organization.Application.DTOs;
using CommunityOS.Organization.Application.Permissions;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.Repositories;
using MediatR;

namespace CommunityOS.Organization.Application.Queries;

public sealed record GetCommitteeByIdQuery(
    Guid ActorId, Guid CommitteeId, DateTime? AsOf = null) : IRequest<CommitteeDto>;

internal sealed class GetCommitteeByIdQueryHandler(
    ICommitteeRepository committees,
    AuthorizationGuard guard) : IRequestHandler<GetCommitteeByIdQuery, CommitteeDto>
{
    public async Task<CommitteeDto> Handle(GetCommitteeByIdQuery request, CancellationToken ct)
    {
        var committee = await committees.GetByIdAsync(request.CommitteeId, ct)
            ?? throw new CommitteeNotFoundException(request.CommitteeId);

        await guard.RequireAsync(
            request.ActorId,
            OrganizationPermissions.CommitteeRead,
            OrganizationContext.For(committee),
            ct);

        return CreateCommitteeCommandHandler.Map(committee, request.AsOf);
    }
}

public sealed record GetCommitteesQuery(
    Guid ActorId, Guid? OrganizationId = null, DateTime? AsOf = null)
    : IRequest<IReadOnlyList<CommitteeDto>>;

internal sealed class GetCommitteesQueryHandler(
    ICommitteeRepository committees,
    IOrganizationRepository organizations,
    AuthorizationGuard guard) : IRequestHandler<GetCommitteesQuery, IReadOnlyList<CommitteeDto>>
{
    public async Task<IReadOnlyList<CommitteeDto>> Handle(GetCommitteesQuery request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, OrganizationPermissions.CommitteeRead, null, ct);

        IReadOnlyList<Domain.Aggregates.Committee> items;
        if (request.OrganizationId is { } organizationId)
        {
            if (await organizations.GetByIdAsync(organizationId, ct) is null)
                throw new OrganizationNotFoundException(organizationId);

            items = await committees.ListByOrganizationAsync(organizationId, ct);
        }
        else
        {
            items = await committees.ListAsync(ct);
        }

        return items
            .Select(c => CreateCommitteeCommandHandler.Map(c, request.AsOf))
            .ToList();
    }
}
