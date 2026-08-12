using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Organization.Application.DTOs;
using CommunityOS.Organization.Application.Permissions;
using CommunityOS.Organization.Application.Pipeline;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.Repositories;
using CommunityOS.Organization.Domain.ValueObjects;
using MediatR;
using DomainEvents = CommunityOS.Organization.Application.Pipeline.DomainEventPublisher;

namespace CommunityOS.Organization.Application.Commands;

using OrganizationUnit = CommunityOS.Organization.Domain.Aggregates.OrganizationUnit;

public sealed record CreateOrganizationUnitCommand(
    Guid ActorId,
    Guid OrganizationId,
    string Name,
    string UnitType,
    Guid? ParentId,
    DateTime EffectiveFrom) : IRequest<OrganizationUnitDto>;

internal sealed class CreateOrganizationUnitCommandHandler(
    IOrganizationUnitRepository units,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<CreateOrganizationUnitCommand, OrganizationUnitDto>
{
    public async Task<OrganizationUnitDto> Handle(CreateOrganizationUnitCommand request, CancellationToken ct)
    {
        var context = request.ParentId is { } parentId
            ? new AuthorizationContext(OrganizationUnitId: parentId)
            : null;
        await guard.RequireAsync(request.ActorId, OrganizationPermissions.UnitCreate, context, ct);

        var period = EffectivePeriod.Create(request.EffectiveFrom);
        var unit = OrganizationUnit.Create(
            request.OrganizationId, request.Name, request.UnitType, request.ParentId, period);

        await units.AddAsync(unit, ct);
        await DomainEvents.PublishAsync(unit, mediator, ct);

        return Map(unit);
    }

    internal static OrganizationUnitDto Map(OrganizationUnit unit) => new(
        unit.Id,
        unit.OrganizationId,
        unit.Name,
        unit.UnitType,
        unit.IsActive,
        unit.ParentIdAt(DateTime.UtcNow),
        unit.CreatedOn);
}

public sealed record UpdateOrganizationUnitCommand(
    Guid ActorId,
    Guid OrganizationUnitId,
    string Name,
    string UnitType) : IRequest<OrganizationUnitDto>;

internal sealed class UpdateOrganizationUnitCommandHandler(
    IOrganizationUnitRepository units,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<UpdateOrganizationUnitCommand, OrganizationUnitDto>
{
    public async Task<OrganizationUnitDto> Handle(UpdateOrganizationUnitCommand request, CancellationToken ct)
    {
        var unit = await units.GetByIdAsync(request.OrganizationUnitId, ct)
            ?? throw new OrganizationUnitNotFoundException(request.OrganizationUnitId);

        await guard.RequireAsync(
            request.ActorId,
            OrganizationPermissions.UnitUpdate,
            new AuthorizationContext(OrganizationUnitId: unit.Id),
            ct);

        unit.UpdateDetails(request.Name, request.UnitType);

        await units.UpdateAsync(unit, ct);
        await DomainEvents.PublishAsync(unit, mediator, ct);

        return CreateOrganizationUnitCommandHandler.Map(unit);
    }
}

public sealed record ChangeOrganizationUnitParentCommand(
    Guid ActorId,
    Guid OrganizationUnitId,
    Guid? ParentId,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil) : IRequest<OrganizationUnitDto>;

internal sealed class ChangeOrganizationUnitParentCommandHandler(
    IOrganizationUnitRepository units,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<ChangeOrganizationUnitParentCommand, OrganizationUnitDto>
{
    public async Task<OrganizationUnitDto> Handle(ChangeOrganizationUnitParentCommand request, CancellationToken ct)
    {
        var unit = await units.GetByIdAsync(request.OrganizationUnitId, ct)
            ?? throw new OrganizationUnitNotFoundException(request.OrganizationUnitId);

        await guard.RequireAsync(
            request.ActorId,
            OrganizationPermissions.UnitReparent,
            new AuthorizationContext(OrganizationUnitId: unit.Id),
            ct);

        if (request.ParentId is { } parentId &&
            await units.IsDescendantAsync(parentId, unit.Id, request.EffectiveFrom, ct))
            throw new HierarchyCycleException(unit.Id);

        var period = EffectivePeriod.Create(request.EffectiveFrom, request.EffectiveUntil);
        unit.ChangeParent(request.ParentId, period);

        await units.UpdateAsync(unit, ct);
        await DomainEvents.PublishAsync(unit, mediator, ct);

        return CreateOrganizationUnitCommandHandler.Map(unit);
    }
}

public sealed record DeactivateOrganizationUnitCommand(
    Guid ActorId,
    Guid OrganizationUnitId) : IRequest<OrganizationUnitDto>;

internal sealed class DeactivateOrganizationUnitCommandHandler(
    IOrganizationUnitRepository units,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<DeactivateOrganizationUnitCommand, OrganizationUnitDto>
{
    public async Task<OrganizationUnitDto> Handle(DeactivateOrganizationUnitCommand request, CancellationToken ct)
    {
        var unit = await units.GetByIdAsync(request.OrganizationUnitId, ct)
            ?? throw new OrganizationUnitNotFoundException(request.OrganizationUnitId);

        await guard.RequireAsync(
            request.ActorId,
            OrganizationPermissions.UnitDeactivate,
            new AuthorizationContext(OrganizationUnitId: unit.Id),
            ct);

        unit.Deactivate();

        await units.UpdateAsync(unit, ct);
        await DomainEvents.PublishAsync(unit, mediator, ct);

        return CreateOrganizationUnitCommandHandler.Map(unit);
    }
}
