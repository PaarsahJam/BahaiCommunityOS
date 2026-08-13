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
[Route("api/v{version:apiVersion}/households")]
[Authorize]
public sealed class HouseholdsController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<HouseholdDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await sender.Send(new GetHouseholdsQuery(ActorId), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<HouseholdDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetHouseholdByIdQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<HouseholdDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateHouseholdCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId }, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<HouseholdDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateHouseholdCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId, HouseholdId = id }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/members")]
    [ProducesResponseType<HouseholdDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMember(
        Guid id, [FromBody] AddHouseholdMemberCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId, HouseholdId = id }, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/members/{personId:guid}")]
    [ProducesResponseType<HouseholdDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid personId, CancellationToken ct)
    {
        var result = await sender.Send(new RemoveHouseholdMemberCommand(ActorId, id, personId), ct);
        return Ok(result);
    }
}