using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Organization.Application.Commands;
using CommunityOS.Organization.Application.DTOs;
using CommunityOS.Organization.Application.Permissions;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.Repositories;
using MediatR;

namespace CommunityOS.Organization.Application.Queries;

public sealed record GetInstitutionByIdQuery(Guid ActorId, Guid InstitutionId)
    : IRequest<InstitutionDto>;

internal sealed class GetInstitutionByIdQueryHandler(
    IInstitutionRepository institutions,
    AuthorizationGuard guard) : IRequestHandler<GetInstitutionByIdQuery, InstitutionDto>
{
    public async Task<InstitutionDto> Handle(GetInstitutionByIdQuery request, CancellationToken ct)
    {
        var institution = await institutions.GetByIdAsync(request.InstitutionId, ct)
            ?? throw new InstitutionNotFoundException(request.InstitutionId);

        await guard.RequireAsync(request.ActorId, OrganizationPermissions.InstitutionRead, null, ct);

        return CreateInstitutionCommandHandler.Map(institution);
    }
}

public sealed record GetInstitutionsQuery(Guid ActorId) : IRequest<IReadOnlyList<InstitutionDto>>;

internal sealed class GetInstitutionsQueryHandler(
    IInstitutionRepository institutions,
    AuthorizationGuard guard) : IRequestHandler<GetInstitutionsQuery, IReadOnlyList<InstitutionDto>>
{
    public async Task<IReadOnlyList<InstitutionDto>> Handle(GetInstitutionsQuery request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, OrganizationPermissions.InstitutionRead, null, ct);

        var items = await institutions.ListAsync(ct);
        return items.Select(CreateInstitutionCommandHandler.Map).ToList();
    }
}