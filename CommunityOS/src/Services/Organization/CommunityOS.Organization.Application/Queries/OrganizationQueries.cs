using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Organization.Application.DTOs;
using CommunityOS.Organization.Application.Permissions;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.Repositories;
using MediatR;

namespace CommunityOS.Organization.Application.Queries;

using Organization = CommunityOS.Organization.Domain.Aggregates.Organization;

public sealed record GetOrganizationByIdQuery(
    Guid ActorId, Guid OrganizationId) : IRequest<OrganizationDto>;

internal sealed class GetOrganizationByIdQueryHandler(
    IOrganizationRepository organizations,
    AuthorizationGuard guard) : IRequestHandler<GetOrganizationByIdQuery, OrganizationDto>
{
    public async Task<OrganizationDto> Handle(GetOrganizationByIdQuery request, CancellationToken ct)
    {
        var organization = await organizations.GetByIdAsync(request.OrganizationId, ct)
            ?? throw new OrganizationNotFoundException(request.OrganizationId);

        await guard.RequireAsync(request.ActorId, OrganizationPermissions.OrgRead, null, ct);

        return Map(organization);
    }

    internal static OrganizationDto Map(Organization organization) => new(
        organization.Id,
        organization.Name,
        organization.OrganizationType,
        organization.Status,
        organization.Jurisdiction.Type.Name,
        organization.Jurisdiction.ScopeId,
        organization.EstablishedOn,
        organization.DissolvedOn,
        organization.CreatedOn);
}

public sealed record GetOrganizationsQuery(Guid ActorId) : IRequest<IReadOnlyList<OrganizationDto>>;

internal sealed class GetOrganizationsQueryHandler(
    IOrganizationRepository organizations,
    AuthorizationGuard guard) : IRequestHandler<GetOrganizationsQuery, IReadOnlyList<OrganizationDto>>
{
    public async Task<IReadOnlyList<OrganizationDto>> Handle(GetOrganizationsQuery request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, OrganizationPermissions.OrgRead, null, ct);

        var items = await organizations.ListAsync(ct);
        return items.Select(GetOrganizationByIdQueryHandler.Map).ToList();
    }
}
