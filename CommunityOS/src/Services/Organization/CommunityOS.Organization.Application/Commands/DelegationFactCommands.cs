using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Organization.Application.DTOs;
using CommunityOS.Organization.Application.Permissions;
using CommunityOS.Organization.Application.Pipeline;
using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.Repositories;
using CommunityOS.Organization.Domain.ValueObjects;
using MediatR;
using DomainEvents = CommunityOS.Organization.Application.Pipeline.DomainEventPublisher;

namespace CommunityOS.Organization.Application.Commands;

public sealed record GrantDelegationFactCommand(
    Guid ActorId,
    Guid DelegatorId,
    Guid DelegateId,
    Guid OrganizationUnitId,
    string DelegationType,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil,
    string? Reason) : IRequest<DelegationFactDto>;

internal sealed class GrantDelegationFactCommandHandler(
    IDelegationFactRepository delegationFacts,
    IOrganizationUnitRepository units,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<GrantDelegationFactCommand, DelegationFactDto>
{
    public async Task<DelegationFactDto> Handle(GrantDelegationFactCommand request, CancellationToken ct)
    {
        if (await units.GetByIdAsync(request.OrganizationUnitId, ct) is null)
            throw new OrganizationUnitNotFoundException(request.OrganizationUnitId);

        await guard.RequireAsync(
            request.ActorId,
            OrganizationPermissions.DelegationGrant,
            new AuthorizationContext(OrganizationUnitId: request.OrganizationUnitId),
            ct);

        var period = EffectivePeriod.Create(request.EffectiveFrom, request.EffectiveUntil);
        var fact = DelegationFact.Create(
            request.DelegatorId,
            request.DelegateId,
            request.OrganizationUnitId,
            request.DelegationType,
            period,
            request.ActorId,
            request.Reason);

        await delegationFacts.AddAsync(fact, ct);
        await DomainEvents.PublishAsync(fact, mediator, ct);

        return Map(fact);
    }

    internal static DelegationFactDto Map(DelegationFact fact, bool includeReason = true) => new(
        fact.Id,
        fact.DelegatorId,
        fact.DelegateId,
        fact.OrganizationUnitId,
        fact.DelegationType,
        fact.Status.Name,
        fact.Period.EffectiveFrom,
        fact.Period.EffectiveUntil,
        fact.GrantedBy,
        fact.GrantedOn,
        fact.RevokedBy,
        fact.RevokedOn,
        includeReason ? fact.Reason : null);
}

public sealed record RevokeDelegationFactCommand(
    Guid ActorId,
    Guid DelegationFactId,
    string? Reason) : IRequest<DelegationFactDto>;

internal sealed class RevokeDelegationFactCommandHandler(
    IDelegationFactRepository delegationFacts,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<RevokeDelegationFactCommand, DelegationFactDto>
{
    public async Task<DelegationFactDto> Handle(RevokeDelegationFactCommand request, CancellationToken ct)
    {
        var fact = await delegationFacts.GetByIdAsync(request.DelegationFactId, ct)
            ?? throw new DelegationFactNotFoundException(request.DelegationFactId);

        await guard.RequireAsync(
            request.ActorId,
            OrganizationPermissions.DelegationRevoke,
            new AuthorizationContext(OrganizationUnitId: fact.OrganizationUnitId),
            ct);

        fact.Revoke(request.ActorId, request.Reason);

        await delegationFacts.UpdateAsync(fact, ct);
        await DomainEvents.PublishAsync(fact, mediator, ct);

        return GrantDelegationFactCommandHandler.Map(fact);
    }
}
