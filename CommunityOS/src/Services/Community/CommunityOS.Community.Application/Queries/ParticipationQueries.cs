using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Application.Permissions;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.Repositories;
using MediatR;

namespace CommunityOS.Community.Application.Queries;

public sealed record GetParticipationByIdQuery(
    Guid ActorId,
    Guid ParticipationId) : IRequest<ParticipationDto>;

internal sealed class GetParticipationByIdQueryHandler(
    IParticipationRepository participations,
    AuthorizationGuard guard) : IRequestHandler<GetParticipationByIdQuery, ParticipationDto>
{
    public async Task<ParticipationDto> Handle(GetParticipationByIdQuery request, CancellationToken ct)
    {
        var participation = await participations.GetByIdAsync(request.ParticipationId, ct)
            ?? throw new ParticipationNotFoundException(request.ParticipationId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.ParticipationRead,
            new AuthorizationContext(ResourceType: "participation", ResourceId: request.ParticipationId), ct);

        return participation.ToDto();
    }
}

public sealed record GetParticipationsByPersonQuery(
    Guid ActorId,
    Guid PersonId) : IRequest<IReadOnlyList<ParticipationDto>>;

internal sealed class GetParticipationsByPersonQueryHandler(
    IParticipationRepository participations,
    AuthorizationGuard guard) : IRequestHandler<GetParticipationsByPersonQuery, IReadOnlyList<ParticipationDto>>
{
    public async Task<IReadOnlyList<ParticipationDto>> Handle(GetParticipationsByPersonQuery request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, CommunityPermissions.ParticipationRead,
            new AuthorizationContext(ResourceType: "person", ResourceId: request.PersonId), ct);

        var all = await participations.ListByPersonAsync(request.PersonId, ct);
        return all.Select(p => p.ToDto()).ToList();
    }
}

public sealed record GetParticipationsByTargetQuery(
    Guid ActorId,
    string TargetType,
    Guid TargetId) : IRequest<IReadOnlyList<ParticipationDto>>;

internal sealed class GetParticipationsByTargetQueryHandler(
    IParticipationRepository participations,
    AuthorizationGuard guard) : IRequestHandler<GetParticipationsByTargetQuery, IReadOnlyList<ParticipationDto>>
{
    public async Task<IReadOnlyList<ParticipationDto>> Handle(GetParticipationsByTargetQuery request, CancellationToken ct)
    {
        var targetType = ParticipationTargetType.FromName(request.TargetType);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.ParticipationRead,
            new AuthorizationContext(ResourceType: targetType.Name, ResourceId: request.TargetId), ct);

        var all = await participations.ListByTargetAsync(targetType, request.TargetId, ct);
        return all.Select(p => p.ToDto()).ToList();
    }
}

public sealed record GetCalendarQuery(
    Guid ActorId,
    DateTime From,
    DateTime To,
    Guid? OrganizationUnitId,
    string? Types) : IRequest<IReadOnlyList<CalendarEntryDto>>;

internal sealed class GetCalendarQueryHandler(
    IActivityRepository activities,
    ICommunityEventRepository events,
    IMeetingRepository meetings,
    AuthorizationGuard guard) : IRequestHandler<GetCalendarQuery, IReadOnlyList<CalendarEntryDto>>
{
    public async Task<IReadOnlyList<CalendarEntryDto>> Handle(GetCalendarQuery request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, CommunityPermissions.CalendarRead,
            new AuthorizationContext(OrganizationUnitId: request.OrganizationUnitId, ResourceType: "calendar"), ct);

        var from = request.From.ToUniversalTime();
        var to = request.To.ToUniversalTime();
        var includeActivities = Includes(request.Types, "activity");
        var includeEvents = Includes(request.Types, "event");
        var includeMeetings = Includes(request.Types, "meeting");

        var entries = new List<CalendarEntryDto>();

        if (includeActivities)
        {
            var list = await activities.ListAsync(from, to, request.OrganizationUnitId, ct);
            entries.AddRange(list.Select(a => new CalendarEntryDto(
                a.Id, "activity", a.Title, a.Schedule.StartsAt, a.Schedule.EndsAt,
                null, a.OrganizationUnitId, a.Visibility.Name, a.Status.Name)));
        }

        if (includeEvents)
        {
            var list = await events.ListAsync(from, to, request.OrganizationUnitId, ct);
            entries.AddRange(list.Select(e => new CalendarEntryDto(
                e.Id, "event", e.Title, e.StartsAt, e.EndsAt,
                e.TimeZone, e.OrganizationUnitId, e.Visibility.Name, e.Status.Name)));
        }

        if (includeMeetings)
        {
            var list = await meetings.ListAsync(from, to, request.OrganizationUnitId, ct);
            entries.AddRange(list.Select(m => new CalendarEntryDto(
                m.Id, "meeting", m.Title, m.StartsAt, m.EndsAt,
                m.TimeZone, m.OrganizationUnitId, m.Visibility.Name, m.Status.Name)));
        }

        return entries.OrderBy(e => e.StartsAt).ToList();
    }

    private static bool Includes(string? types, string candidate)
    {
        if (string.IsNullOrWhiteSpace(types))
            return true;

        return types
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(candidate, StringComparer.OrdinalIgnoreCase);
    }
}
