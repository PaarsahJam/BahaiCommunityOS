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
[Route("api/v{version:apiVersion}/participations")]
[Authorize]
public sealed class ParticipationsController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ParticipationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetParticipationByIdQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpGet("by-person/{personId:guid}")]
    [ProducesResponseType<IReadOnlyList<ParticipationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByPerson(Guid personId, CancellationToken ct)
    {
        var result = await sender.Send(new GetParticipationsByPersonQuery(ActorId, personId), ct);
        return Ok(result);
    }

    [HttpGet("by-target")]
    [ProducesResponseType<IReadOnlyList<ParticipationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByTarget(
        [FromQuery] string targetType, [FromQuery] Guid targetId, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetParticipationsByTargetQuery(ActorId, targetType, targetId), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<ParticipationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Record(
        [FromBody] RecordParticipationCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId }, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<ParticipationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid id, [FromBody] UpdateStatusRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateParticipationStatusCommand(ActorId, id, request.Status), ct);
        return Ok(result);
    }
}

public sealed record UpdateStatusRequest(string Status);