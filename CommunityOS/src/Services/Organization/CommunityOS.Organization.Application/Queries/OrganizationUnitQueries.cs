using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Organization.Application.DTOs;
using CommunityOS.Organization.Application.Permissions;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.Repositories;
using MediatR;

namespace CommunityOS.Organization.Application.Queries;

using OrganizationUnit = CommunityOS.Organization.Domain.Aggregates.OrganizationUnit;

public sealed record GetOrganizationUnitByIdQuery(
    Guid ActorId, Guid OrganizationUnitId, DateTime? AsOf = null) : IRequest<OrganizationUnitDetailDto>;

internal sealed class GetOrganizationUnitByIdQueryHandler(
    IOrganizationUnitRepository units,
    AuthorizationGuard guard) : IRequestHandler<GetOrganizationUnitByIdQuery, OrganizationUnitDetailDto>
{
    public async Task<OrganizationUnitDetailDto> Handle(GetOrganizationUnitByIdQuery request, CancellationToken ct)
    {
        var unit = await units.GetByIdAsync(request.OrganizationUnitId, ct)
            ?? throw new OrganizationUnitNotFoundException(request.OrganizationUnitId);

        await guard.RequireAsync(
            request.ActorId,
            OrganizationPermissions.UnitRead,
            new AuthorizationContext(OrganizationUnitId: unit.Id),
            ct);

        var moment = request.AsOf ?? DateTime.UtcNow;
        var children = await units.ListChildrenAsync(unit.Id, moment, ct);

        return new OrganizationUnitDetailDto(
            Map(unit, moment),
            unit.Parents
                .OrderByDescending(p => p.Period.EffectiveFrom)
                .Select(p => new OrganizationUnitParentDto(
                    p.ParentId, p.Period.EffectiveFrom, p.Period.EffectiveUntil))
                .ToList(),
            children.Select(c => GetOrganizationUnitByIdQueryHandler.Map(c, moment)).ToList());
    }

    internal static OrganizationUnitDto Map(OrganizationUnit unit, DateTime moment) => new(
        unit.Id,
        unit.OrganizationId,
        unit.Name,
        unit.UnitType,
        unit.IsActive,
        unit.ParentIdAt(moment),
        unit.CreatedOn);
}

public sealed record GetOrganizationUnitsQuery(
    Guid ActorId, Guid OrganizationId, DateTime? AsOf = null)
    : IRequest<IReadOnlyList<OrganizationUnitDto>>;

internal sealed class GetOrganizationUnitsQueryHandler(
    IOrganizationUnitRepository units,
    AuthorizationGuard guard) : IRequestHandler<GetOrganizationUnitsQuery, IReadOnlyList<OrganizationUnitDto>>
{
    public async Task<IReadOnlyList<OrganizationUnitDto>> Handle(GetOrganizationUnitsQuery request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, OrganizationPermissions.UnitRead, null, ct);

        var moment = request.AsOf ?? DateTime.UtcNow;
        var items = await units.ListByOrganizationAsync(request.OrganizationId, ct);
        return items.Select(u => GetOrganizationUnitByIdQueryHandler.Map(u, moment)).ToList();
    }
}

public sealed record GetOrganizationUnitChildrenQuery(
    Guid ActorId, Guid ParentId, DateTime? AsOf = null)
    : IRequest<IReadOnlyList<OrganizationUnitDto>>;

internal sealed class GetOrganizationUnitChildrenQueryHandler(
    IOrganizationUnitRepository units,
    AuthorizationGuard guard) : IRequestHandler<GetOrganizationUnitChildrenQuery, IReadOnlyList<OrganizationUnitDto>>
{
    public async Task<IReadOnlyList<OrganizationUnitDto>> Handle(GetOrganizationUnitChildrenQuery request, CancellationToken ct)
    {
        await guard.RequireAsync(
            request.ActorId,
            OrganizationPermissions.UnitRead,
            new AuthorizationContext(OrganizationUnitId: request.ParentId),
            ct);

        var moment = request.AsOf ?? DateTime.UtcNow;
        var items = await units.ListChildrenAsync(request.ParentId, moment, ct);
        return items.Select(u => GetOrganizationUnitByIdQueryHandler.Map(u, moment)).ToList();
    }
}

public sealed record GetOrganizationUnitDescendantsQuery(
    Guid ActorId, Guid OrganizationUnitId, DateTime? AsOf = null)
    : IRequest<IReadOnlyList<OrganizationUnitDto>>;

internal sealed class GetOrganizationUnitDescendantsQueryHandler(
    IOrganizationUnitRepository units,
    AuthorizationGuard guard) : IRequestHandler<GetOrganizationUnitDescendantsQuery, IReadOnlyList<OrganizationUnitDto>>
{
    public async Task<IReadOnlyList<OrganizationUnitDto>> Handle(GetOrganizationUnitDescendantsQuery request, CancellationToken ct)
    {
        await guard.RequireAsync(
            request.ActorId,
            OrganizationPermissions.UnitRead,
            new AuthorizationContext(OrganizationUnitId: request.OrganizationUnitId),
            ct);

        var moment = request.AsOf ?? DateTime.UtcNow;
        var items = await units.ListDescendantsAsync(request.OrganizationUnitId, moment, ct);
        return items.Select(u => GetOrganizationUnitByIdQueryHandler.Map(u, moment)).ToList();
    }
}

public sealed record GetOrganizationUnitAncestorsQuery(
    Guid ActorId, Guid OrganizationUnitId, DateTime? AsOf = null)
    : IRequest<IReadOnlyList<OrganizationUnitDto>>;

internal sealed class GetOrganizationUnitAncestorsQueryHandler(
    IOrganizationUnitRepository units,
    AuthorizationGuard guard) : IRequestHandler<GetOrganizationUnitAncestorsQuery, IReadOnlyList<OrganizationUnitDto>>
{
    public async Task<IReadOnlyList<OrganizationUnitDto>> Handle(GetOrganizationUnitAncestorsQuery request, CancellationToken ct)
    {
        await guard.RequireAsync(
            request.ActorId,
            OrganizationPermissions.UnitRead,
            new AuthorizationContext(OrganizationUnitId: request.OrganizationUnitId),
            ct);

        var moment = request.AsOf ?? DateTime.UtcNow;
        var ancestorIds = await units.ListAncestorIdsAsync(request.OrganizationUnitId, moment, ct);

        var ancestors = new List<OrganizationUnitDto>(ancestorIds.Count);
        foreach (var ancestorId in ancestorIds)
        {
            var ancestor = await units.GetByIdAsync(ancestorId, ct);
            if (ancestor is not null)
                ancestors.Add(GetOrganizationUnitByIdQueryHandler.Map(ancestor, moment));
        }

        return ancestors;
    }
}

/// <summary>
/// Narrow internal fact query used by the Authorization service to resolve
/// organization-scope grants. It answers whether
/// <paramref name="CandidateAncestorId"/> is the same unit as, or an ancestor
/// of, <paramref name="OrganizationUnitId"/> at <paramref name="AsOf"/>.
///
/// The query intentionally performs no application-layer permission check: it
/// is an internal cross-service fact and the trust boundary is enforced at the
/// API edge (a service-only endpoint guarded by client authentication, fail
/// closed). Routing this through the standard permission guard would create a
/// request cycle (Authorization resolves scope by calling Organization, which
/// would re-enter Authorization to authorize the call).
/// </summary>
public sealed record GetOrganizationUnitCoveredQuery(
    Guid OrganizationUnitId,
    Guid CandidateAncestorId,
    DateTime? AsOf = null) : IRequest<bool>;

internal sealed class GetOrganizationUnitCoveredQueryHandler(
    IOrganizationUnitRepository units) : IRequestHandler<GetOrganizationUnitCoveredQuery, bool>
{
    public async Task<bool> Handle(GetOrganizationUnitCoveredQuery request, CancellationToken ct)
    {
        var moment = request.AsOf ?? DateTime.UtcNow;
        return await units.IsDescendantAsync(request.OrganizationUnitId, request.CandidateAncestorId, moment, ct);
    }
}
