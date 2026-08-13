using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Application.Permissions;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.Repositories;
using MediatR;

namespace CommunityOS.Community.Application.Queries;

public sealed record GetHouseholdByIdQuery(
    Guid ActorId,
    Guid HouseholdId) : IRequest<HouseholdDto>;

internal sealed class GetHouseholdByIdQueryHandler(
    IHouseholdRepository households,
    AuthorizationGuard guard) : IRequestHandler<GetHouseholdByIdQuery, HouseholdDto>
{
    public async Task<HouseholdDto> Handle(GetHouseholdByIdQuery request, CancellationToken ct)
    {
        var household = await households.GetByIdAsync(request.HouseholdId, ct)
            ?? throw new HouseholdNotFoundException(request.HouseholdId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.HouseholdRead,
            new AuthorizationContext(ResourceType: "household", ResourceId: request.HouseholdId), ct);

        return household.ToDto();
    }
}

public sealed record GetHouseholdsQuery(
    Guid ActorId) : IRequest<IReadOnlyList<HouseholdDto>>;

internal sealed class GetHouseholdsQueryHandler(
    IHouseholdRepository households,
    AuthorizationGuard guard) : IRequestHandler<GetHouseholdsQuery, IReadOnlyList<HouseholdDto>>
{
    public async Task<IReadOnlyList<HouseholdDto>> Handle(GetHouseholdsQuery request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, CommunityPermissions.HouseholdRead, null, ct);

        var all = await households.ListAsync(ct);
        return all.Select(h => h.ToDto()).ToList();
    }
}

public sealed record GetFamilyRelationshipsQuery(
    Guid ActorId,
    Guid PersonId) : IRequest<IReadOnlyList<FamilyRelationshipDto>>;

internal sealed class GetFamilyRelationshipsQueryHandler(
    IFamilyRelationshipRepository relationships,
    AuthorizationGuard guard) : IRequestHandler<GetFamilyRelationshipsQuery, IReadOnlyList<FamilyRelationshipDto>>
{
    public async Task<IReadOnlyList<FamilyRelationshipDto>> Handle(GetFamilyRelationshipsQuery request, CancellationToken ct)
    {
        // Family relationships are sensitive: an explicit grant is required in
        // addition to the person read.
        await guard.RequireAsync(request.ActorId, CommunityPermissions.FamilyRead,
            new AuthorizationContext(ResourceType: "person", ResourceId: request.PersonId), ct);

        var all = await relationships.ListByPersonAsync(request.PersonId, ct);
        return all.Select(r => r.ToDto()).ToList();
    }
}
