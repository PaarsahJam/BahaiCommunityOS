using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Organization.Application.DTOs;
using CommunityOS.Organization.Application.Permissions;
using CommunityOS.Organization.Application.Pipeline;
using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Enumerations;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.Repositories;
using CommunityOS.Organization.Domain.ValueObjects;
using MediatR;
using DomainEvents = CommunityOS.Organization.Application.Pipeline.DomainEventPublisher;

namespace CommunityOS.Organization.Application.Commands;

public sealed record CreateCommitteeCommand(
    Guid ActorId,
    string Name,
    string CommitteeType,
    Guid OrganizationId,
    Guid? OrganizationUnitId,
    string JurisdictionType,
    Guid? JurisdictionScopeId) : IRequest<CommitteeDto>;

internal sealed class CreateCommitteeCommandHandler(
    ICommitteeRepository committees,
    IOrganizationRepository organizations,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<CreateCommitteeCommand, CommitteeDto>
{
    public async Task<CommitteeDto> Handle(CreateCommitteeCommand request, CancellationToken ct)
    {
        if (await organizations.GetByIdAsync(request.OrganizationId, ct) is null)
            throw new OrganizationNotFoundException(request.OrganizationId);

        var context = request.OrganizationUnitId is { } unitId
            ? new AuthorizationContext(OrganizationUnitId: unitId)
            : null;
        await guard.RequireAsync(request.ActorId, OrganizationPermissions.CommitteeCreate, context, ct);

        var jurisdiction = Jurisdiction.Create(
            JurisdictionType.FromName(request.JurisdictionType), request.JurisdictionScopeId);

        var committee = Committee.Create(
            request.Name, request.CommitteeType, request.OrganizationId, request.OrganizationUnitId, jurisdiction);

        await committees.AddAsync(committee, ct);
        await DomainEvents.PublishAsync(committee, mediator, ct);

        return Map(committee);
    }

    internal static CommitteeDto Map(Committee committee, DateTime? asOf = null) => new(
        committee.Id,
        committee.Name,
        committee.CommitteeType,
        committee.OrganizationId,
        committee.OrganizationUnitId,
        committee.Jurisdiction.Type.Name,
        committee.Jurisdiction.ScopeId,
        committee.IsActive,
        committee.CreatedOn,
        committee.Members
            .Where(m => asOf is null || m.IsEffectiveAt(asOf.Value.ToUniversalTime()))
            .Select(m => new CommitteeMemberDto(
                m.PersonId, m.RoleCode, m.Period.EffectiveFrom, m.Period.EffectiveUntil))
            .ToList());
}

public sealed record UpdateCommitteeCommand(
    Guid ActorId,
    Guid CommitteeId,
    string Name,
    string JurisdictionType,
    Guid? JurisdictionScopeId) : IRequest<CommitteeDto>;

internal sealed class UpdateCommitteeCommandHandler(
    ICommitteeRepository committees,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<UpdateCommitteeCommand, CommitteeDto>
{
    public async Task<CommitteeDto> Handle(UpdateCommitteeCommand request, CancellationToken ct)
    {
        var committee = await committees.GetByIdAsync(request.CommitteeId, ct)
            ?? throw new CommitteeNotFoundException(request.CommitteeId);

        await guard.RequireAsync(
            request.ActorId,
            OrganizationPermissions.CommitteeUpdate,
            OrganizationContext.For(committee),
            ct);

        var jurisdiction = Jurisdiction.Create(
            JurisdictionType.FromName(request.JurisdictionType), request.JurisdictionScopeId);
        committee.UpdateDetails(request.Name, jurisdiction);

        await committees.UpdateAsync(committee, ct);
        await DomainEvents.PublishAsync(committee, mediator, ct);

        return CreateCommitteeCommandHandler.Map(committee);
    }
}

public sealed record AddCommitteeMemberCommand(
    Guid ActorId,
    Guid CommitteeId,
    Guid PersonId,
    string RoleCode,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil) : IRequest<CommitteeDto>;

internal sealed class AddCommitteeMemberCommandHandler(
    ICommitteeRepository committees,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<AddCommitteeMemberCommand, CommitteeDto>
{
    public async Task<CommitteeDto> Handle(AddCommitteeMemberCommand request, CancellationToken ct)
    {
        var committee = await committees.GetByIdAsync(request.CommitteeId, ct)
            ?? throw new CommitteeNotFoundException(request.CommitteeId);

        await guard.RequireAsync(
            request.ActorId,
            OrganizationPermissions.CommitteeMemberAdd,
            OrganizationContext.For(committee),
            ct);

        var period = EffectivePeriod.Create(request.EffectiveFrom, request.EffectiveUntil);
        committee.AddMember(request.PersonId, request.RoleCode, period);

        await committees.UpdateAsync(committee, ct);
        await DomainEvents.PublishAsync(committee, mediator, ct);

        return CreateCommitteeCommandHandler.Map(committee);
    }
}

public sealed record RemoveCommitteeMemberCommand(
    Guid ActorId,
    Guid CommitteeId,
    Guid PersonId,
    string RoleCode) : IRequest<CommitteeDto>;

internal sealed class RemoveCommitteeMemberCommandHandler(
    ICommitteeRepository committees,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<RemoveCommitteeMemberCommand, CommitteeDto>
{
    public async Task<CommitteeDto> Handle(RemoveCommitteeMemberCommand request, CancellationToken ct)
    {
        var committee = await committees.GetByIdAsync(request.CommitteeId, ct)
            ?? throw new CommitteeNotFoundException(request.CommitteeId);

        await guard.RequireAsync(
            request.ActorId,
            OrganizationPermissions.CommitteeMemberRemove,
            OrganizationContext.For(committee),
            ct);

        committee.RemoveMember(request.PersonId, request.RoleCode);

        await committees.UpdateAsync(committee, ct);
        await DomainEvents.PublishAsync(committee, mediator, ct);

        return CreateCommitteeCommandHandler.Map(committee);
    }
}

public sealed record DeactivateCommitteeCommand(
    Guid ActorId,
    Guid CommitteeId) : IRequest<CommitteeDto>;

internal sealed class DeactivateCommitteeCommandHandler(
    ICommitteeRepository committees,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<DeactivateCommitteeCommand, CommitteeDto>
{
    public async Task<CommitteeDto> Handle(DeactivateCommitteeCommand request, CancellationToken ct)
    {
        var committee = await committees.GetByIdAsync(request.CommitteeId, ct)
            ?? throw new CommitteeNotFoundException(request.CommitteeId);

        await guard.RequireAsync(
            request.ActorId,
            OrganizationPermissions.CommitteeDeactivate,
            OrganizationContext.For(committee),
            ct);

        committee.Deactivate();

        await committees.UpdateAsync(committee, ct);
        await DomainEvents.PublishAsync(committee, mediator, ct);

        return CreateCommitteeCommandHandler.Map(committee);
    }
}

/// <summary>
/// Derives a guard authorization context from a committee's owning
/// organization unit. Falls back to a global (no unit) context when the
/// committee is not attached to a specific unit.
/// </summary>
internal static class OrganizationContext
{
    public static AuthorizationContext? For(Committee committee) =>
        committee.OrganizationUnitId is { } unitId
            ? new AuthorizationContext(OrganizationUnitId: unitId)
            : null;
}
