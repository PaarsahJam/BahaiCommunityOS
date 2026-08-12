using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Organization.Application.DTOs;
using CommunityOS.Organization.Application.Permissions;
using CommunityOS.Organization.Application.Pipeline;
using CommunityOS.Organization.Domain.Enumerations;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.Repositories;
using CommunityOS.Organization.Domain.ValueObjects;
using MediatR;
using DomainEvents = CommunityOS.Organization.Application.Pipeline.DomainEventPublisher;

namespace CommunityOS.Organization.Application.Commands;

using Organization = CommunityOS.Organization.Domain.Aggregates.Organization;

public sealed record CreateOrganizationCommand(
    Guid ActorId,
    string Name,
    string OrganizationType,
    string JurisdictionType,
    Guid? JurisdictionScopeId,
    DateTime? EstablishedOn) : IRequest<OrganizationDto>;

internal sealed class CreateOrganizationCommandHandler(
    IOrganizationRepository organizations,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<CreateOrganizationCommand, OrganizationDto>
{
    public async Task<OrganizationDto> Handle(CreateOrganizationCommand request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, OrganizationPermissions.OrgCreate, null, ct);

        if (await organizations.GetByNameAsync(request.Name, ct) is not null)
            throw new OrganizationNameAlreadyExistsException(request.Name);

        var jurisdiction = Jurisdiction.Create(
            JurisdictionType.FromName(request.JurisdictionType), request.JurisdictionScopeId);

        var organization = Organization.Create(
            request.Name, request.OrganizationType, jurisdiction, request.EstablishedOn);

        await organizations.AddAsync(organization, ct);
        await DomainEvents.PublishAsync(organization, mediator, ct);

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

public sealed record UpdateOrganizationCommand(
    Guid ActorId,
    Guid OrganizationId,
    string Name,
    string JurisdictionType,
    Guid? JurisdictionScopeId) : IRequest<OrganizationDto>;

internal sealed class UpdateOrganizationCommandHandler(
    IOrganizationRepository organizations,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<UpdateOrganizationCommand, OrganizationDto>
{
    public async Task<OrganizationDto> Handle(UpdateOrganizationCommand request, CancellationToken ct)
    {
        var organization = await organizations.GetByIdAsync(request.OrganizationId, ct)
            ?? throw new OrganizationNotFoundException(request.OrganizationId);

        await guard.RequireAsync(request.ActorId, OrganizationPermissions.OrgUpdate, null, ct);

        var jurisdiction = Jurisdiction.Create(
            JurisdictionType.FromName(request.JurisdictionType), request.JurisdictionScopeId);
        organization.UpdateDetails(request.Name, jurisdiction);

        await organizations.UpdateAsync(organization, ct);
        await DomainEvents.PublishAsync(organization, mediator, ct);

        return CreateOrganizationCommandHandler.Map(organization);
    }
}

public sealed record DissolveOrganizationCommand(
    Guid ActorId,
    Guid OrganizationId,
    DateTime DissolvedOn) : IRequest<OrganizationDto>;

internal sealed class DissolveOrganizationCommandHandler(
    IOrganizationRepository organizations,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<DissolveOrganizationCommand, OrganizationDto>
{
    public async Task<OrganizationDto> Handle(DissolveOrganizationCommand request, CancellationToken ct)
    {
        var organization = await organizations.GetByIdAsync(request.OrganizationId, ct)
            ?? throw new OrganizationNotFoundException(request.OrganizationId);

        await guard.RequireAsync(request.ActorId, OrganizationPermissions.OrgDissolve, null, ct);

        organization.Dissolve(request.DissolvedOn);

        await organizations.UpdateAsync(organization, ct);
        await DomainEvents.PublishAsync(organization, mediator, ct);

        return CreateOrganizationCommandHandler.Map(organization);
    }
}
