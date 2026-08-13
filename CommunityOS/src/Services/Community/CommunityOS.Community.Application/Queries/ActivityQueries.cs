using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Application.Permissions;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.Repositories;
using MediatR;

namespace CommunityOS.Community.Application.Queries;

public sealed record GetActivityByIdQuery(
    Guid ActorId,
    Guid ActivityId) : IRequest<ActivityDto>;

internal sealed class GetActivityByIdQueryHandler(
    IActivityRepository activities,
    AuthorizationGuard guard) : IRequestHandler<GetActivityByIdQuery, ActivityDto>
{
    public async Task<ActivityDto> Handle(GetActivityByIdQuery request, CancellationToken ct)
    {
        var activity = await activities.GetByIdAsync(request.ActivityId, ct)
            ?? throw new ActivityNotFoundException(request.ActivityId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.ActivityRead,
            new AuthorizationContext(
                OrganizationUnitId: activity.OrganizationUnitId,
                ResourceType: "activity",
                ResourceId: request.ActivityId), ct);

        return activity.ToDto();
    }
}

public sealed record GetActivitiesQuery(
    Guid ActorId,
    DateTime? From,
    DateTime? To,
    Guid? OrganizationUnitId) : IRequest<IReadOnlyList<ActivityDto>>;

internal sealed class GetActivitiesQueryHandler(
    IActivityRepository activities,
    AuthorizationGuard guard) : IRequestHandler<GetActivitiesQuery, IReadOnlyList<ActivityDto>>
{
    public async Task<IReadOnlyList<ActivityDto>> Handle(GetActivitiesQuery request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, CommunityPermissions.ActivityRead,
            new AuthorizationContext(OrganizationUnitId: request.OrganizationUnitId, ResourceType: "activity"), ct);

        var all = await activities.ListAsync(request.From, request.To, request.OrganizationUnitId, ct);
        return all.Select(a => a.ToDto()).ToList();
    }
}

public sealed record GetCommunityEventByIdQuery(
    Guid ActorId,
    Guid EventId) : IRequest<CommunityEventDto>;

internal sealed class GetCommunityEventByIdQueryHandler(
    ICommunityEventRepository events,
    AuthorizationGuard guard) : IRequestHandler<GetCommunityEventByIdQuery, CommunityEventDto>
{
    public async Task<CommunityEventDto> Handle(GetCommunityEventByIdQuery request, CancellationToken ct)
    {
        var communityEvent = await events.GetByIdAsync(request.EventId, ct)
            ?? throw new CommunityEventNotFoundException(request.EventId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.EventRead,
            new AuthorizationContext(
                OrganizationUnitId: communityEvent.OrganizationUnitId,
                ResourceType: "event",
                ResourceId: request.EventId), ct);

        return communityEvent.ToDto();
    }
}

public sealed record GetCommunityEventsQuery(
    Guid ActorId,
    DateTime? From,
    DateTime? To,
    Guid? OrganizationUnitId) : IRequest<IReadOnlyList<CommunityEventDto>>;

internal sealed class GetCommunityEventsQueryHandler(
    ICommunityEventRepository events,
    AuthorizationGuard guard) : IRequestHandler<GetCommunityEventsQuery, IReadOnlyList<CommunityEventDto>>
{
    public async Task<IReadOnlyList<CommunityEventDto>> Handle(GetCommunityEventsQuery request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, CommunityPermissions.EventRead,
            new AuthorizationContext(OrganizationUnitId: request.OrganizationUnitId, ResourceType: "event"), ct);

        var all = await events.ListAsync(request.From, request.To, request.OrganizationUnitId, ct);
        return all.Select(e => e.ToDto()).ToList();
    }
}

public sealed record GetMeetingByIdQuery(
    Guid ActorId,
    Guid MeetingId) : IRequest<MeetingDto>;

internal sealed class GetMeetingByIdQueryHandler(
    IMeetingRepository meetings,
    AuthorizationGuard guard) : IRequestHandler<GetMeetingByIdQuery, MeetingDto>
{
    public async Task<MeetingDto> Handle(GetMeetingByIdQuery request, CancellationToken ct)
    {
        var meeting = await meetings.GetByIdAsync(request.MeetingId, ct)
            ?? throw new MeetingNotFoundException(request.MeetingId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.MeetingRead,
            new AuthorizationContext(
                OrganizationUnitId: meeting.OrganizationUnitId,
                ResourceType: "meeting",
                ResourceId: request.MeetingId), ct);

        return meeting.ToDto();
    }
}

public sealed record GetMeetingsQuery(
    Guid ActorId,
    DateTime? From,
    DateTime? To,
    Guid? OrganizationUnitId) : IRequest<IReadOnlyList<MeetingDto>>;

internal sealed class GetMeetingsQueryHandler(
    IMeetingRepository meetings,
    AuthorizationGuard guard) : IRequestHandler<GetMeetingsQuery, IReadOnlyList<MeetingDto>>
{
    public async Task<IReadOnlyList<MeetingDto>> Handle(GetMeetingsQuery request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, CommunityPermissions.MeetingRead,
            new AuthorizationContext(OrganizationUnitId: request.OrganizationUnitId, ResourceType: "meeting"), ct);

        var all = await meetings.ListAsync(request.From, request.To, request.OrganizationUnitId, ct);
        return all.Select(m => m.ToDto()).ToList();
    }
}
