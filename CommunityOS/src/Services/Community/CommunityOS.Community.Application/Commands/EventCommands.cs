using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Application.Permissions;
using CommunityOS.Community.Application.Pipeline;
using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.Repositories;
using MediatR;
using DomainEvents = CommunityOS.Community.Application.Pipeline.DomainEventPublisher;

namespace CommunityOS.Community.Application.Commands;

public sealed record CreateCommunityEventCommand(
    Guid ActorId,
    string Title,
    string? Description,
    DateTime StartsAt,
    DateTime? EndsAt,
    string TimeZone,
    string? Location,
    bool IsOnline,
    string? OnlineUrl,
    Guid? OrganizerPersonId,
    Guid? OrganizationUnitId,
    string Status,
    string Visibility,
    bool RegistrationOpen,
    int? Capacity) : IRequest<CommunityEventDto>;

internal sealed class CreateCommunityEventCommandHandler(
    ICommunityEventRepository events,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<CreateCommunityEventCommand, CommunityEventDto>
{
    public async Task<CommunityEventDto> Handle(CreateCommunityEventCommand request, CancellationToken ct)
    {
        var context = new AuthorizationContext(
            OrganizationUnitId: request.OrganizationUnitId,
            ResourceType: "event");
        await guard.RequireAsync(request.ActorId, CommunityPermissions.EventCreate, context, ct);

        var communityEvent = CommunityEvent.Create(
            request.Title,
            request.Description,
            request.StartsAt,
            request.EndsAt,
            request.TimeZone,
            request.Location,
            request.IsOnline,
            request.OnlineUrl,
            request.OrganizerPersonId,
            request.OrganizationUnitId,
            CommunityEventStatus.FromName(request.Status),
            CommunityEventVisibility.FromName(request.Visibility),
            request.RegistrationOpen,
            request.Capacity);

        await events.AddAsync(communityEvent, ct);
        await DomainEvents.PublishAsync(communityEvent, mediator, ct);

        return communityEvent.ToDto();
    }
}

public sealed record UpdateCommunityEventCommand(
    Guid ActorId,
    Guid EventId,
    string Title,
    string? Description,
    string? Location,
    bool IsOnline,
    string? OnlineUrl,
    string Visibility,
    bool RegistrationOpen) : IRequest<CommunityEventDto>;

internal sealed class UpdateCommunityEventCommandHandler(
    ICommunityEventRepository events,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<UpdateCommunityEventCommand, CommunityEventDto>
{
    public async Task<CommunityEventDto> Handle(UpdateCommunityEventCommand request, CancellationToken ct)
    {
        var communityEvent = await events.GetByIdAsync(request.EventId, ct)
            ?? throw new CommunityEventNotFoundException(request.EventId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.EventUpdate,
            new AuthorizationContext(
                OrganizationUnitId: communityEvent.OrganizationUnitId,
                ResourceType: "event",
                ResourceId: request.EventId), ct);

        communityEvent.UpdateDetails(
            request.Title,
            request.Description,
            request.Location,
            request.IsOnline,
            request.OnlineUrl,
            CommunityEventVisibility.FromName(request.Visibility),
            request.RegistrationOpen);

        await events.UpdateAsync(communityEvent, ct);
        await DomainEvents.PublishAsync(communityEvent, mediator, ct);

        return communityEvent.ToDto();
    }
}

public sealed record RescheduleCommunityEventCommand(
    Guid ActorId,
    Guid EventId,
    DateTime StartsAt,
    DateTime? EndsAt) : IRequest<CommunityEventDto>;

internal sealed class RescheduleCommunityEventCommandHandler(
    ICommunityEventRepository events,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<RescheduleCommunityEventCommand, CommunityEventDto>
{
    public async Task<CommunityEventDto> Handle(RescheduleCommunityEventCommand request, CancellationToken ct)
    {
        var communityEvent = await events.GetByIdAsync(request.EventId, ct)
            ?? throw new CommunityEventNotFoundException(request.EventId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.EventUpdate,
            new AuthorizationContext(
                OrganizationUnitId: communityEvent.OrganizationUnitId,
                ResourceType: "event",
                ResourceId: request.EventId), ct);

        communityEvent.Reschedule(request.StartsAt, request.EndsAt);

        await events.UpdateAsync(communityEvent, ct);
        await DomainEvents.PublishAsync(communityEvent, mediator, ct);

        return communityEvent.ToDto();
    }
}

public sealed record CancelCommunityEventCommand(
    Guid ActorId,
    Guid EventId) : IRequest<CommunityEventDto>;

internal sealed class CancelCommunityEventCommandHandler(
    ICommunityEventRepository events,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<CancelCommunityEventCommand, CommunityEventDto>
{
    public async Task<CommunityEventDto> Handle(CancelCommunityEventCommand request, CancellationToken ct)
    {
        var communityEvent = await events.GetByIdAsync(request.EventId, ct)
            ?? throw new CommunityEventNotFoundException(request.EventId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.EventUpdate,
            new AuthorizationContext(
                OrganizationUnitId: communityEvent.OrganizationUnitId,
                ResourceType: "event",
                ResourceId: request.EventId), ct);

        communityEvent.Cancel();

        await events.UpdateAsync(communityEvent, ct);
        await DomainEvents.PublishAsync(communityEvent, mediator, ct);

        return communityEvent.ToDto();
    }
}
