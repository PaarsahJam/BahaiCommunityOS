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

public sealed record CreateInstitutionCommand(
    Guid ActorId,
    string Name,
    string InstitutionType,
    string JurisdictionType,
    Guid? JurisdictionScopeId,
    DateTime? EstablishedOn) : IRequest<InstitutionDto>;

internal sealed class CreateInstitutionCommandHandler(
    IInstitutionRepository institutions,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<CreateInstitutionCommand, InstitutionDto>
{
    public async Task<InstitutionDto> Handle(CreateInstitutionCommand request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, OrganizationPermissions.InstitutionCreate, null, ct);

        if (await institutions.GetByNameAsync(request.Name, ct) is not null)
            throw new OrganizationNameAlreadyExistsException(request.Name);

        var jurisdiction = Jurisdiction.Create(
            JurisdictionType.FromName(request.JurisdictionType), request.JurisdictionScopeId);

        var institution = Institution.Create(
            request.Name, request.InstitutionType, jurisdiction, request.EstablishedOn);

        await institutions.AddAsync(institution, ct);
        await DomainEvents.PublishAsync(institution, mediator, ct);

        return Map(institution);
    }

    internal static InstitutionDto Map(Institution institution) => new(
        institution.Id,
        institution.Name,
        institution.InstitutionType,
        institution.Jurisdiction.Type.Name,
        institution.Jurisdiction.ScopeId,
        institution.IsActive,
        institution.EstablishedOn,
        institution.CreatedOn);
}

public sealed record UpdateInstitutionCommand(
    Guid ActorId,
    Guid InstitutionId,
    string Name,
    string JurisdictionType,
    Guid? JurisdictionScopeId) : IRequest<InstitutionDto>;

internal sealed class UpdateInstitutionCommandHandler(
    IInstitutionRepository institutions,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<UpdateInstitutionCommand, InstitutionDto>
{
    public async Task<InstitutionDto> Handle(UpdateInstitutionCommand request, CancellationToken ct)
    {
        var institution = await institutions.GetByIdAsync(request.InstitutionId, ct)
            ?? throw new InstitutionNotFoundException(request.InstitutionId);

        await guard.RequireAsync(request.ActorId, OrganizationPermissions.InstitutionUpdate, null, ct);

        var jurisdiction = Jurisdiction.Create(
            JurisdictionType.FromName(request.JurisdictionType), request.JurisdictionScopeId);
        institution.UpdateDetails(request.Name, jurisdiction);

        await institutions.UpdateAsync(institution, ct);
        await DomainEvents.PublishAsync(institution, mediator, ct);

        return CreateInstitutionCommandHandler.Map(institution);
    }
}
