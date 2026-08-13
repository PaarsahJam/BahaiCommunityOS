using Asp.Versioning;
using CommunityOS.Community.API.Extensions;
using CommunityOS.Community.Application.Commands;
using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Community.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/meetings")]
[Authorize]
public sealed class MeetingsController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MeetingDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? organizationUnitId,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetMeetingsQuery(ActorId, from, to, organizationUnitId), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<MeetingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetMeetingByIdQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<MeetingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateMeetingCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId }, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<MeetingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateMeetingCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId, MeetingId = id }, ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/schedule")]
    [ProducesResponseType<MeetingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reschedule(
        Guid id, [FromBody] RescheduleRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new RescheduleMeetingCommand(ActorId, id, request.StartsAt, request.EndsAt), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/participants")]
    [ProducesResponseType<MeetingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddParticipant(
        Guid id, [FromBody] AddMeetingParticipantCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId, MeetingId = id }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/attendance")]
    [ProducesResponseType<MeetingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordAttendance(
        Guid id, [FromBody] RecordAttendanceRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new RecordMeetingAttendanceCommand(ActorId, id, request.PersonId, request.Attendance), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/agenda-items")]
    [ProducesResponseType<MeetingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddAgendaItem(
        Guid id, [FromBody] AddMeetingAgendaItemCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId, MeetingId = id }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/actions")]
    [ProducesResponseType<MeetingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddAction(
        Guid id, [FromBody] AddMeetingActionCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId, MeetingId = id }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/minutes")]
    [ProducesResponseType<MeetingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordMinutes(
        Guid id, [FromBody] RecordMinutesRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new RecordMeetingMinutesCommand(ActorId, id, request.Minutes), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<MeetingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new CancelMeetingCommand(ActorId, id), ct);
        return Ok(result);
    }
}

public sealed record RecordAttendanceRequest(Guid PersonId, string Attendance);

public sealed record RecordMinutesRequest(string Minutes);