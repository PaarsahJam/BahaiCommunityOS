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
[Route("api/v{version:apiVersion}/community-events")]
[Authorize]
public sealed class CommunityEventsController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CommunityEventDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? organizationUnitId,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetCommunityEventsQuery(ActorId, from, to, organizationUnitId), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CommunityEventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetCommunityEventByIdQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<CommunityEventDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCommunityEventCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId }, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<CommunityEventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateCommunityEventCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId, EventId = id }, ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/schedule")]
    [ProducesResponseType<CommunityEventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reschedule(
        Guid id, [FromBody] RescheduleRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new RescheduleCommunityEventCommand(ActorId, id, request.StartsAt, request.EndsAt), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<CommunityEventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new CancelCommunityEventCommand(ActorId, id), ct);
        return Ok(result);
    }
}