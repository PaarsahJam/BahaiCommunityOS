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

public sealed record CreateActivityCommand(
    Guid ActorId,
    string Title,
    string? Description,
    string? Category,
    Guid? OrganizerPersonId,
    Guid? OrganizationUnitId,
    string? Location,
    bool IsOnline,
    string? OnlineUrl,
    DateTime StartsAt,
    DateTime? EndsAt,
    string Visibility,
    string Status,
    int? Capacity) : IRequest<ActivityDto>;

internal sealed class CreateActivityCommandHandler(
    IActivityRepository activities,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<CreateActivityCommand, ActivityDto>
{
    public async Task<ActivityDto> Handle(CreateActivityCommand request, CancellationToken ct)
    {
        var context = new AuthorizationContext(
            OrganizationUnitId: request.OrganizationUnitId,
            ResourceType: "activity");
        await guard.RequireAsync(request.ActorId, CommunityPermissions.ActivityCreate, context, ct);

        var activity = Activity.Create(
            request.Title,
            request.Description,
            request.Category,
            request.OrganizerPersonId,
            request.OrganizationUnitId,
            request.Location,
            request.IsOnline,
            request.OnlineUrl,
            DateTimeRange.Create(request.StartsAt, request.EndsAt),
            ActivityVisibility.FromName(request.Visibility),
            ActivityStatus.FromName(request.Status),
            request.Capacity);

        await activities.AddAsync(activity, ct);
        await DomainEvents.PublishAsync(activity, mediator, ct);

        return activity.ToDto();
    }
}

public sealed record UpdateActivityCommand(
    Guid ActorId,
    Guid ActivityId,
    string Title,
    string? Description,
    string? Category,
    Guid? OrganizerPersonId,
    string? Location,
    bool IsOnline,
    string? OnlineUrl,
    string Visibility) : IRequest<ActivityDto>;

internal sealed class UpdateActivityCommandHandler(
    IActivityRepository activities,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<UpdateActivityCommand, ActivityDto>
{
    public async Task<ActivityDto> Handle(UpdateActivityCommand request, CancellationToken ct)
    {
        var activity = await activities.GetByIdAsync(request.ActivityId, ct)
            ?? throw new ActivityNotFoundException(request.ActivityId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.ActivityUpdate,
            new AuthorizationContext(
                OrganizationUnitId: activity.OrganizationUnitId,
                ResourceType: "activity",
                ResourceId: request.ActivityId), ct);

        activity.UpdateDetails(
            request.Title,
            request.Description,
            request.Category,
            request.OrganizerPersonId,
            request.Location,
            request.IsOnline,
            request.OnlineUrl,
            ActivityVisibility.FromName(request.Visibility));

        await activities.UpdateAsync(activity, ct);
        await DomainEvents.PublishAsync(activity, mediator, ct);

        return activity.ToDto();
    }
}

public sealed record RescheduleActivityCommand(
    Guid ActorId,
    Guid ActivityId,
    DateTime StartsAt,
    DateTime? EndsAt) : IRequest<ActivityDto>;

internal sealed class RescheduleActivityCommandHandler(
    IActivityRepository activities,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<RescheduleActivityCommand, ActivityDto>
{
    public async Task<ActivityDto> Handle(RescheduleActivityCommand request, CancellationToken ct)
    {
        var activity = await activities.GetByIdAsync(request.ActivityId, ct)
            ?? throw new ActivityNotFoundException(request.ActivityId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.ActivityUpdate,
            new AuthorizationContext(
                OrganizationUnitId: activity.OrganizationUnitId,
                ResourceType: "activity",
                ResourceId: request.ActivityId), ct);

        activity.ChangeSchedule(DateTimeRange.Create(request.StartsAt, request.EndsAt));

        await activities.UpdateAsync(activity, ct);
        await DomainEvents.PublishAsync(activity, mediator, ct);

        return activity.ToDto();
    }
}

public sealed record CancelActivityCommand(
    Guid ActorId,
    Guid ActivityId) : IRequest<ActivityDto>;

internal sealed class CancelActivityCommandHandler(
    IActivityRepository activities,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<CancelActivityCommand, ActivityDto>
{
    public async Task<ActivityDto> Handle(CancelActivityCommand request, CancellationToken ct)
    {
        var activity = await activities.GetByIdAsync(request.ActivityId, ct)
            ?? throw new ActivityNotFoundException(request.ActivityId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.ActivityUpdate,
            new AuthorizationContext(
                OrganizationUnitId: activity.OrganizationUnitId,
                ResourceType: "activity",
                ResourceId: request.ActivityId), ct);

        activity.Cancel();

        await activities.UpdateAsync(activity, ct);
        await DomainEvents.PublishAsync(activity, mediator, ct);

        return activity.ToDto();
    }
}
