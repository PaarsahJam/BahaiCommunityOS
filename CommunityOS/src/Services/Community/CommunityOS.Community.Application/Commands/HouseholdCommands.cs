using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Application.Permissions;
using CommunityOS.Community.Application.Pipeline;
using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Domain.ValueObjects;
using MediatR;
using DomainEvents = CommunityOS.Community.Application.Pipeline.DomainEventPublisher;

namespace CommunityOS.Community.Application.Commands;

public sealed record CreateHouseholdCommand(
    Guid ActorId,
    string? Name,
    string? Line1,
    string? Line2,
    string? City,
    string? Region,
    string? PostalCode,
    string? Country) : IRequest<HouseholdDto>;

internal sealed class CreateHouseholdCommandHandler(
    IHouseholdRepository households,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<CreateHouseholdCommand, HouseholdDto>
{
    public async Task<HouseholdDto> Handle(CreateHouseholdCommand request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, CommunityPermissions.HouseholdCreate, null, ct);

        var household = Household.Create(
            request.Name,
            PostalAddressFrom(request));

        await households.AddAsync(household, ct);
        await DomainEvents.PublishAsync(household, mediator, ct);

        return household.ToDto();
    }

    internal static PostalAddress? PostalAddressFrom(CreateHouseholdCommand request) =>
        string.IsNullOrWhiteSpace(request.Country)
            ? null
            : PostalAddress.Create(
                request.Line1,
                request.Line2,
                request.City,
                request.Region,
                request.PostalCode,
                request.Country!);
}

public sealed record UpdateHouseholdCommand(
    Guid ActorId,
    Guid HouseholdId,
    string? Name,
    string? Line1,
    string? Line2,
    string? City,
    string? Region,
    string? PostalCode,
    string? Country) : IRequest<HouseholdDto>;

internal sealed class UpdateHouseholdCommandHandler(
    IHouseholdRepository households,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<UpdateHouseholdCommand, HouseholdDto>
{
    public async Task<HouseholdDto> Handle(UpdateHouseholdCommand request, CancellationToken ct)
    {
        var household = await households.GetByIdAsync(request.HouseholdId, ct)
            ?? throw new HouseholdNotFoundException(request.HouseholdId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.HouseholdUpdate,
            new AuthorizationContext(ResourceType: "household", ResourceId: request.HouseholdId), ct);

        household.Rename(request.Name);
        household.ChangeAddress(
            string.IsNullOrWhiteSpace(request.Country)
                ? null
                : PostalAddress.Create(
                    request.Line1,
                    request.Line2,
                    request.City,
                    request.Region,
                    request.PostalCode,
                    request.Country!));

        await households.UpdateAsync(household, ct);
        await DomainEvents.PublishAsync(household, mediator, ct);

        return household.ToDto();
    }
}

public sealed record AddHouseholdMemberCommand(
    Guid ActorId,
    Guid HouseholdId,
    Guid PersonId,
    string Role,
    DateTime EffectiveFrom) : IRequest<HouseholdDto>;

internal sealed class AddHouseholdMemberCommandHandler(
    IHouseholdRepository households,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<AddHouseholdMemberCommand, HouseholdDto>
{
    public async Task<HouseholdDto> Handle(AddHouseholdMemberCommand request, CancellationToken ct)
    {
        var household = await households.GetByIdAsync(request.HouseholdId, ct)
            ?? throw new HouseholdNotFoundException(request.HouseholdId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.HouseholdUpdate,
            new AuthorizationContext(ResourceType: "household", ResourceId: request.HouseholdId), ct);

        household.AddMember(
            request.PersonId,
            HouseholdMemberRole.FromName(request.Role),
            EffectivePeriod.Create(request.EffectiveFrom));

        await households.UpdateAsync(household, ct);
        await DomainEvents.PublishAsync(household, mediator, ct);

        return household.ToDto();
    }
}

public sealed record RemoveHouseholdMemberCommand(
    Guid ActorId,
    Guid HouseholdId,
    Guid PersonId) : IRequest<HouseholdDto>;

internal sealed class RemoveHouseholdMemberCommandHandler(
    IHouseholdRepository households,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<RemoveHouseholdMemberCommand, HouseholdDto>
{
    public async Task<HouseholdDto> Handle(RemoveHouseholdMemberCommand request, CancellationToken ct)
    {
        var household = await households.GetByIdAsync(request.HouseholdId, ct)
            ?? throw new HouseholdNotFoundException(request.HouseholdId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.HouseholdUpdate,
            new AuthorizationContext(ResourceType: "household", ResourceId: request.HouseholdId), ct);

        household.RemoveMember(request.PersonId);

        await households.UpdateAsync(household, ct);
        await DomainEvents.PublishAsync(household, mediator, ct);

        return household.ToDto();
    }
}

public sealed record CreateFamilyRelationshipCommand(
    Guid ActorId,
    Guid PersonIdA,
    Guid PersonIdB,
    string RelationshipType,
    DateTime EffectiveFrom) : IRequest<FamilyRelationshipDto>;

internal sealed class CreateFamilyRelationshipCommandHandler(
    IFamilyRelationshipRepository relationships,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<CreateFamilyRelationshipCommand, FamilyRelationshipDto>
{
    public async Task<FamilyRelationshipDto> Handle(CreateFamilyRelationshipCommand request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, CommunityPermissions.FamilyCreate, null, ct);

        var type = RelationshipType.FromName(request.RelationshipType);
        if (await relationships.ExistsActiveAsync(request.PersonIdA, request.PersonIdB, type, ct))
            throw new DuplicateFamilyRelationshipException(request.PersonIdA, request.PersonIdB, type.Name);

        var relationship = FamilyRelationship.Create(
            request.PersonIdA,
            request.PersonIdB,
            type,
            EffectivePeriod.Create(request.EffectiveFrom));

        await relationships.AddAsync(relationship, ct);
        await DomainEvents.PublishAsync(relationship, mediator, ct);

        return relationship.ToDto();
    }
}

public sealed record EndFamilyRelationshipCommand(
    Guid ActorId,
    Guid RelationshipId,
    DateTime EndedOn) : IRequest<FamilyRelationshipDto>;

internal sealed class EndFamilyRelationshipCommandHandler(
    IFamilyRelationshipRepository relationships,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<EndFamilyRelationshipCommand, FamilyRelationshipDto>
{
    public async Task<FamilyRelationshipDto> Handle(EndFamilyRelationshipCommand request, CancellationToken ct)
    {
        var relationship = await relationships.GetByIdAsync(request.RelationshipId, ct)
            ?? throw new FamilyRelationshipNotFoundException(request.RelationshipId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.FamilyUpdate,
            new AuthorizationContext(ResourceType: "family_relationship", ResourceId: request.RelationshipId), ct);

        relationship.End(request.EndedOn);

        await relationships.UpdateAsync(relationship, ct);
        await DomainEvents.PublishAsync(relationship, mediator, ct);

        return relationship.ToDto();
    }
}
