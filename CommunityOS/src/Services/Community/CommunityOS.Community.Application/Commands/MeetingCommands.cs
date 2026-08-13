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

public sealed record CreateMeetingCommand(
    Guid ActorId,
    string Title,
    string? Description,
    DateTime StartsAt,
    DateTime? EndsAt,
    string TimeZone,
    string? Location,
    Guid? OrganizerPersonId,
    Guid? OrganizationUnitId,
    string Status,
    string Visibility) : IRequest<MeetingDto>;

internal sealed class CreateMeetingCommandHandler(
    IMeetingRepository meetings,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<CreateMeetingCommand, MeetingDto>
{
    public async Task<MeetingDto> Handle(CreateMeetingCommand request, CancellationToken ct)
    {
        var context = new AuthorizationContext(
            OrganizationUnitId: request.OrganizationUnitId,
            ResourceType: "meeting");
        await guard.RequireAsync(request.ActorId, CommunityPermissions.MeetingCreate, context, ct);

        var meeting = Meeting.Create(
            request.Title,
            request.Description,
            request.StartsAt,
            request.EndsAt,
            request.TimeZone,
            request.Location,
            request.OrganizerPersonId,
            request.OrganizationUnitId,
            MeetingStatus.FromName(request.Status),
            MeetingVisibility.FromName(request.Visibility));

        await meetings.AddAsync(meeting, ct);
        await DomainEvents.PublishAsync(meeting, mediator, ct);

        return meeting.ToDto();
    }
}

public sealed record UpdateMeetingCommand(
    Guid ActorId,
    Guid MeetingId,
    string Title,
    string? Description,
    string? Location,
    string Visibility) : IRequest<MeetingDto>;

internal sealed class UpdateMeetingCommandHandler(
    IMeetingRepository meetings,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<UpdateMeetingCommand, MeetingDto>
{
    public async Task<MeetingDto> Handle(UpdateMeetingCommand request, CancellationToken ct)
    {
        var meeting = await meetings.GetByIdAsync(request.MeetingId, ct)
            ?? throw new MeetingNotFoundException(request.MeetingId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.MeetingUpdate,
            new AuthorizationContext(
                OrganizationUnitId: meeting.OrganizationUnitId,
                ResourceType: "meeting",
                ResourceId: request.MeetingId), ct);

        meeting.UpdateDetails(
            request.Title,
            request.Description,
            request.Location,
            MeetingVisibility.FromName(request.Visibility));

        await meetings.UpdateAsync(meeting, ct);
        await DomainEvents.PublishAsync(meeting, mediator, ct);

        return meeting.ToDto();
    }
}

public sealed record RescheduleMeetingCommand(
    Guid ActorId,
    Guid MeetingId,
    DateTime StartsAt,
    DateTime? EndsAt) : IRequest<MeetingDto>;

internal sealed class RescheduleMeetingCommandHandler(
    IMeetingRepository meetings,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<RescheduleMeetingCommand, MeetingDto>
{
    public async Task<MeetingDto> Handle(RescheduleMeetingCommand request, CancellationToken ct)
    {
        var meeting = await meetings.GetByIdAsync(request.MeetingId, ct)
            ?? throw new MeetingNotFoundException(request.MeetingId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.MeetingUpdate,
            new AuthorizationContext(
                OrganizationUnitId: meeting.OrganizationUnitId,
                ResourceType: "meeting",
                ResourceId: request.MeetingId), ct);

        meeting.Reschedule(request.StartsAt, request.EndsAt);

        await meetings.UpdateAsync(meeting, ct);
        await DomainEvents.PublishAsync(meeting, mediator, ct);

        return meeting.ToDto();
    }
}

public sealed record AddMeetingParticipantCommand(
    Guid ActorId,
    Guid MeetingId,
    Guid PersonId,
    string Role) : IRequest<MeetingDto>;

internal sealed class AddMeetingParticipantCommandHandler(
    IMeetingRepository meetings,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<AddMeetingParticipantCommand, MeetingDto>
{
    public async Task<MeetingDto> Handle(AddMeetingParticipantCommand request, CancellationToken ct)
    {
        var meeting = await meetings.GetByIdAsync(request.MeetingId, ct)
            ?? throw new MeetingNotFoundException(request.MeetingId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.MeetingParticipantManage,
            new AuthorizationContext(
                OrganizationUnitId: meeting.OrganizationUnitId,
                ResourceType: "meeting",
                ResourceId: request.MeetingId), ct);

        meeting.AddParticipant(request.PersonId, request.Role);

        await meetings.UpdateAsync(meeting, ct);
        await DomainEvents.PublishAsync(meeting, mediator, ct);

        return meeting.ToDto();
    }
}

public sealed record RecordMeetingAttendanceCommand(
    Guid ActorId,
    Guid MeetingId,
    Guid PersonId,
    string Attendance) : IRequest<MeetingDto>;

internal sealed class RecordMeetingAttendanceCommandHandler(
    IMeetingRepository meetings,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<RecordMeetingAttendanceCommand, MeetingDto>
{
    public async Task<MeetingDto> Handle(RecordMeetingAttendanceCommand request, CancellationToken ct)
    {
        var meeting = await meetings.GetByIdAsync(request.MeetingId, ct)
            ?? throw new MeetingNotFoundException(request.MeetingId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.MeetingParticipantManage,
            new AuthorizationContext(
                OrganizationUnitId: meeting.OrganizationUnitId,
                ResourceType: "meeting",
                ResourceId: request.MeetingId), ct);

        meeting.RecordAttendance(request.PersonId, AttendanceStatus.FromName(request.Attendance));

        await meetings.UpdateAsync(meeting, ct);
        await DomainEvents.PublishAsync(meeting, mediator, ct);

        return meeting.ToDto();
    }
}

public sealed record AddMeetingAgendaItemCommand(
    Guid ActorId,
    Guid MeetingId,
    string Title,
    string? Description,
    int Order) : IRequest<MeetingDto>;

internal sealed class AddMeetingAgendaItemCommandHandler(
    IMeetingRepository meetings,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<AddMeetingAgendaItemCommand, MeetingDto>
{
    public async Task<MeetingDto> Handle(AddMeetingAgendaItemCommand request, CancellationToken ct)
    {
        var meeting = await meetings.GetByIdAsync(request.MeetingId, ct)
            ?? throw new MeetingNotFoundException(request.MeetingId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.MeetingUpdate,
            new AuthorizationContext(
                OrganizationUnitId: meeting.OrganizationUnitId,
                ResourceType: "meeting",
                ResourceId: request.MeetingId), ct);

        meeting.AddAgendaItem(request.Title, request.Description, request.Order);

        await meetings.UpdateAsync(meeting, ct);
        await DomainEvents.PublishAsync(meeting, mediator, ct);

        return meeting.ToDto();
    }
}

public sealed record AddMeetingActionCommand(
    Guid ActorId,
    Guid MeetingId,
    string Description,
    Guid? AssigneePersonId,
    DateTime? DueDate) : IRequest<MeetingDto>;

internal sealed class AddMeetingActionCommandHandler(
    IMeetingRepository meetings,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<AddMeetingActionCommand, MeetingDto>
{
    public async Task<MeetingDto> Handle(AddMeetingActionCommand request, CancellationToken ct)
    {
        var meeting = await meetings.GetByIdAsync(request.MeetingId, ct)
            ?? throw new MeetingNotFoundException(request.MeetingId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.MeetingUpdate,
            new AuthorizationContext(
                OrganizationUnitId: meeting.OrganizationUnitId,
                ResourceType: "meeting",
                ResourceId: request.MeetingId), ct);

        meeting.AddAction(request.Description, request.AssigneePersonId, request.DueDate);

        await meetings.UpdateAsync(meeting, ct);
        await DomainEvents.PublishAsync(meeting, mediator, ct);

        return meeting.ToDto();
    }
}

public sealed record RecordMeetingMinutesCommand(
    Guid ActorId,
    Guid MeetingId,
    string Minutes) : IRequest<MeetingDto>;

internal sealed class RecordMeetingMinutesCommandHandler(
    IMeetingRepository meetings,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<RecordMeetingMinutesCommand, MeetingDto>
{
    public async Task<MeetingDto> Handle(RecordMeetingMinutesCommand request, CancellationToken ct)
    {
        var meeting = await meetings.GetByIdAsync(request.MeetingId, ct)
            ?? throw new MeetingNotFoundException(request.MeetingId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.MeetingRecord,
            new AuthorizationContext(
                OrganizationUnitId: meeting.OrganizationUnitId,
                ResourceType: "meeting",
                ResourceId: request.MeetingId), ct);

        meeting.RecordMinutes(request.Minutes);

        await meetings.UpdateAsync(meeting, ct);
        await DomainEvents.PublishAsync(meeting, mediator, ct);

        return meeting.ToDto();
    }
}

public sealed record CancelMeetingCommand(
    Guid ActorId,
    Guid MeetingId) : IRequest<MeetingDto>;

internal sealed class CancelMeetingCommandHandler(
    IMeetingRepository meetings,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<CancelMeetingCommand, MeetingDto>
{
    public async Task<MeetingDto> Handle(CancelMeetingCommand request, CancellationToken ct)
    {
        var meeting = await meetings.GetByIdAsync(request.MeetingId, ct)
            ?? throw new MeetingNotFoundException(request.MeetingId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.MeetingUpdate,
            new AuthorizationContext(
                OrganizationUnitId: meeting.OrganizationUnitId,
                ResourceType: "meeting",
                ResourceId: request.MeetingId), ct);

        meeting.Cancel();

        await meetings.UpdateAsync(meeting, ct);
        await DomainEvents.PublishAsync(meeting, mediator, ct);

        return meeting.ToDto();
    }
}
