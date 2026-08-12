using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Organization.Application.Commands;
using CommunityOS.Organization.Application.DTOs;
using CommunityOS.Organization.Application.Permissions;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.Repositories;
using MediatR;

namespace CommunityOS.Organization.Application.Queries;

public sealed record GetDelegationFactByIdQuery(Guid ActorId, Guid DelegationFactId)
    : IRequest<DelegationFactDto>;

internal sealed class GetDelegationFactByIdQueryHandler(
    IDelegationFactRepository delegationFacts,
    AuthorizationGuard guard) : IRequestHandler<GetDelegationFactByIdQuery, DelegationFactDto>
{
    public async Task<DelegationFactDto> Handle(GetDelegationFactByIdQuery request, CancellationToken ct)
    {
        var fact = await delegationFacts.GetByIdAsync(request.DelegationFactId, ct)
            ?? throw new DelegationFactNotFoundException(request.DelegationFactId);

        var context = new AuthorizationContext(OrganizationUnitId: fact.OrganizationUnitId);
        await guard.RequireAsync(request.ActorId, OrganizationPermissions.DelegationRead, context, ct);

        var includeReason = await guard.HasAsync(
            request.ActorId, OrganizationPermissions.DelegationReadReason, context, ct);

        return GrantDelegationFactCommandHandler.Map(fact, includeReason);
    }
}

public sealed record GetDelegationFactsQuery(
    Guid ActorId,
    Guid? DelegatorId = null,
    Guid? DelegateId = null,
    Guid? OrganizationUnitId = null) : IRequest<IReadOnlyList<DelegationFactDto>>;

internal sealed class GetDelegationFactsQueryHandler(
    IDelegationFactRepository delegationFacts,
    AuthorizationGuard guard) : IRequestHandler<GetDelegationFactsQuery, IReadOnlyList<DelegationFactDto>>
{
    public async Task<IReadOnlyList<DelegationFactDto>> Handle(GetDelegationFactsQuery request, CancellationToken ct)
    {
        IReadOnlyList<Domain.Aggregates.DelegationFact> items;
        AuthorizationContext? reasonContext = null;
        if (request.DelegatorId is { } delegatorId)
        {
            // Person-scoped delegation lookups are cross-cutting and require an
            // explicit grant (fail closed for the default read permission).
            await guard.RequireAsync(request.ActorId, OrganizationPermissions.DelegationReadPerson, null, ct);
            items = await delegationFacts.ListByDelegatorAsync(delegatorId, ct);
        }
        else if (request.DelegateId is { } delegateId)
        {
            await guard.RequireAsync(request.ActorId, OrganizationPermissions.DelegationReadPerson, null, ct);
            items = await delegationFacts.ListByDelegateAsync(delegateId, ct);
        }
        else if (request.OrganizationUnitId is { } unitId)
        {
            reasonContext = new AuthorizationContext(OrganizationUnitId: unitId);
            await guard.RequireAsync(
                request.ActorId,
                OrganizationPermissions.DelegationRead,
                reasonContext,
                ct);
            items = await delegationFacts.ListByOrganizationUnitAsync(unitId, ct);
        }
        else
        {
            throw new InvalidOperationException(
                "At least one of DelegatorId, DelegateId or OrganizationUnitId must be provided.");
        }

        var includeReason = await guard.HasAsync(
            request.ActorId, OrganizationPermissions.DelegationReadReason, reasonContext, ct);

        return items
            .Select(f => GrantDelegationFactCommandHandler.Map(f, includeReason))
            .ToList();
    }
}
