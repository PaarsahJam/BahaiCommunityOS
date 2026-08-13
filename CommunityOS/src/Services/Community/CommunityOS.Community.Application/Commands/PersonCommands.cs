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

public sealed record CreatePersonCommand(
    Guid ActorId,
    string PreferredName,
    string? FormalName,
    string? PreferredLanguage,
    string ProfileVisibility,
    string ContactVisibility,
    string DateOfBirthVisibility) : IRequest<PersonDto>;

internal sealed class CreatePersonCommandHandler(
    IPersonRepository persons,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<CreatePersonCommand, PersonDto>
{
    public async Task<PersonDto> Handle(CreatePersonCommand request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, CommunityPermissions.PersonCreate, null, ct);

        var privacy = PrivacyPreferences.Create(
            ContactVisibility.FromName(request.ProfileVisibility),
            ContactVisibility.FromName(request.ContactVisibility),
            ContactVisibility.FromName(request.DateOfBirthVisibility));

        var person = Person.Create(
            request.PreferredName,
            request.FormalName,
            request.PreferredLanguage,
            privacy);

        await persons.AddAsync(person, ct);
        await DomainEvents.PublishAsync(person, mediator, ct);

        return person.ToDto();
    }
}

public sealed record UpdatePersonProfileCommand(
    Guid ActorId,
    Guid PersonId,
    string PreferredName,
    string? FormalName,
    DateTime? DateOfBirth,
    string? PreferredLanguage) : IRequest<PersonDetailDto>;

internal sealed class UpdatePersonProfileCommandHandler(
    IPersonRepository persons,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<UpdatePersonProfileCommand, PersonDetailDto>
{
    public async Task<PersonDetailDto> Handle(UpdatePersonProfileCommand request, CancellationToken ct)
    {
        var person = await persons.GetByIdAsync(request.PersonId, ct)
            ?? throw new PersonNotFoundException(request.PersonId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.PersonUpdate,
            new AuthorizationContext(ResourceType: "person", ResourceId: request.PersonId), ct);

        person.UpdateProfile(
            request.PreferredName,
            request.FormalName,
            request.DateOfBirth,
            request.PreferredLanguage);

        await persons.UpdateAsync(person, ct);
        await DomainEvents.PublishAsync(person, mediator, ct);

        var canReadContact = await guard.HasAsync(request.ActorId, CommunityPermissions.PersonContactRead,
            new AuthorizationContext(ResourceType: "person", ResourceId: request.PersonId), ct);
        var canReadSensitive = await guard.HasAsync(request.ActorId, CommunityPermissions.PersonSensitiveRead,
            new AuthorizationContext(ResourceType: "person", ResourceId: request.PersonId), ct);

        return person.ToDetailDto(canReadContact, canReadSensitive);
    }
}

public sealed record SetPersonContactMethodsCommand(
    Guid ActorId,
    Guid PersonId,
    IReadOnlyList<ContactMethodInput> ContactMethods) : IRequest<PersonDetailDto>;

public sealed record ContactMethodInput(
    string Type,
    string Value,
    bool IsPreferred,
    string Visibility);

internal sealed class SetPersonContactMethodsCommandHandler(
    IPersonRepository persons,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<SetPersonContactMethodsCommand, PersonDetailDto>
{
    public async Task<PersonDetailDto> Handle(SetPersonContactMethodsCommand request, CancellationToken ct)
    {
        var person = await persons.GetByIdAsync(request.PersonId, ct)
            ?? throw new PersonNotFoundException(request.PersonId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.PersonUpdate,
            new AuthorizationContext(ResourceType: "person", ResourceId: request.PersonId), ct);

        var methods = request.ContactMethods
            .Select(m => Domain.Entities.ContactMethod.Create(
                ContactMethodType.FromName(m.Type),
                m.Value,
                m.IsPreferred,
                ContactVisibility.FromName(m.Visibility)))
            .ToList();

        person.SetContactMethods(methods);

        await persons.UpdateAsync(person, ct);
        await DomainEvents.PublishAsync(person, mediator, ct);

        var canReadContact = await guard.HasAsync(request.ActorId, CommunityPermissions.PersonContactRead,
            new AuthorizationContext(ResourceType: "person", ResourceId: request.PersonId), ct);
        var canReadSensitive = await guard.HasAsync(request.ActorId, CommunityPermissions.PersonSensitiveRead,
            new AuthorizationContext(ResourceType: "person", ResourceId: request.PersonId), ct);

        return person.ToDetailDto(canReadContact, canReadSensitive);
    }
}

public sealed record LinkPersonToIdentityAccountCommand(
    Guid ActorId,
    Guid PersonId,
    Guid IdentityAccountId) : IRequest<PersonDto>;

internal sealed class LinkPersonToIdentityAccountCommandHandler(
    IPersonRepository persons,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<LinkPersonToIdentityAccountCommand, PersonDto>
{
    public async Task<PersonDto> Handle(LinkPersonToIdentityAccountCommand request, CancellationToken ct)
    {
        var person = await persons.GetByIdAsync(request.PersonId, ct)
            ?? throw new PersonNotFoundException(request.PersonId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.PersonIdentityLink,
            new AuthorizationContext(ResourceType: "person", ResourceId: request.PersonId), ct);

        person.LinkIdentityAccount(request.IdentityAccountId);

        await persons.UpdateAsync(person, ct);
        await DomainEvents.PublishAsync(person, mediator, ct);

        return person.ToDto();
    }
}

public sealed record UnlinkPersonFromIdentityAccountCommand(
    Guid ActorId,
    Guid PersonId) : IRequest<PersonDto>;

internal sealed class UnlinkPersonFromIdentityAccountCommandHandler(
    IPersonRepository persons,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<UnlinkPersonFromIdentityAccountCommand, PersonDto>
{
    public async Task<PersonDto> Handle(UnlinkPersonFromIdentityAccountCommand request, CancellationToken ct)
    {
        var person = await persons.GetByIdAsync(request.PersonId, ct)
            ?? throw new PersonNotFoundException(request.PersonId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.PersonIdentityLink,
            new AuthorizationContext(ResourceType: "person", ResourceId: request.PersonId), ct);

        person.UnlinkIdentityAccount();

        await persons.UpdateAsync(person, ct);
        await DomainEvents.PublishAsync(person, mediator, ct);

        return person.ToDto();
    }
}

public sealed record DeactivatePersonCommand(
    Guid ActorId,
    Guid PersonId) : IRequest<PersonDto>;

internal sealed class DeactivatePersonCommandHandler(
    IPersonRepository persons,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<DeactivatePersonCommand, PersonDto>
{
    public async Task<PersonDto> Handle(DeactivatePersonCommand request, CancellationToken ct)
    {
        var person = await persons.GetByIdAsync(request.PersonId, ct)
            ?? throw new PersonNotFoundException(request.PersonId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.PersonDeactivate,
            new AuthorizationContext(ResourceType: "person", ResourceId: request.PersonId), ct);

        person.Deactivate();

        await persons.UpdateAsync(person, ct);
        await DomainEvents.PublishAsync(person, mediator, ct);

        return person.ToDto();
    }
}

public sealed record ReactivatePersonCommand(
    Guid ActorId,
    Guid PersonId) : IRequest<PersonDto>;

internal sealed class ReactivatePersonCommandHandler(
    IPersonRepository persons,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<ReactivatePersonCommand, PersonDto>
{
    public async Task<PersonDto> Handle(ReactivatePersonCommand request, CancellationToken ct)
    {
        var person = await persons.GetByIdAsync(request.PersonId, ct)
            ?? throw new PersonNotFoundException(request.PersonId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.PersonUpdate,
            new AuthorizationContext(ResourceType: "person", ResourceId: request.PersonId), ct);

        person.Reactivate();

        await persons.UpdateAsync(person, ct);
        await DomainEvents.PublishAsync(person, mediator, ct);

        return person.ToDto();
    }
}
