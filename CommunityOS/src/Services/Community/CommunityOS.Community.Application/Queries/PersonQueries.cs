using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Application.Permissions;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.Repositories;
using MediatR;

namespace CommunityOS.Community.Application.Queries;

public sealed record GetPersonByIdQuery(
    Guid ActorId,
    Guid PersonId) : IRequest<PersonDetailDto>;

internal sealed class GetPersonByIdQueryHandler(
    IPersonRepository persons,
    AuthorizationGuard guard) : IRequestHandler<GetPersonByIdQuery, PersonDetailDto>
{
    public async Task<PersonDetailDto> Handle(GetPersonByIdQuery request, CancellationToken ct)
    {
        var person = await persons.GetByIdAsync(request.PersonId, ct)
            ?? throw new PersonNotFoundException(request.PersonId);

        var context = new AuthorizationContext(ResourceType: "person", ResourceId: request.PersonId);
        await guard.RequireAsync(request.ActorId, CommunityPermissions.PersonRead, context, ct);

        // Privacy-scoped grants gate contact details and sensitive attributes.
        var canReadContact = await guard.HasAsync(request.ActorId, CommunityPermissions.PersonContactRead, context, ct);
        var canReadSensitive = await guard.HasAsync(request.ActorId, CommunityPermissions.PersonSensitiveRead, context, ct);

        return person.ToDetailDto(canReadContact, canReadSensitive);
    }
}

public sealed record GetPersonsQuery(
    Guid ActorId) : IRequest<IReadOnlyList<PersonDto>>;

internal sealed class GetPersonsQueryHandler(
    IPersonRepository persons,
    AuthorizationGuard guard) : IRequestHandler<GetPersonsQuery, IReadOnlyList<PersonDto>>
{
    public async Task<IReadOnlyList<PersonDto>> Handle(GetPersonsQuery request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, CommunityPermissions.PersonRead, null, ct);

        var all = await persons.ListAsync(ct);
        return all.Select(p => p.ToDto()).ToList();
    }
}
