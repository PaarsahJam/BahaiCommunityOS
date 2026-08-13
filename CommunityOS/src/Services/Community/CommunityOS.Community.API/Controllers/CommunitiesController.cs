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
[Route("api/v{version:apiVersion}/communities")]
[Authorize]
public sealed class CommunitiesController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CommunityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetCommunityByIdQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CommunityDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByParent(
        [FromQuery] Guid parentId, CancellationToken ct)
    {
        var result = await sender.Send(new GetCommunitiesByParentQuery(ActorId, parentId), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<CommunityDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCommunityCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId }, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateCommunityCommand cmd, CancellationToken ct)
    {
        await sender.Send(cmd with { ActorId = ActorId, CommunityId = id }, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/local-units")]
    [ProducesResponseType<LocalUnitDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddLocalUnit(
        Guid id, [FromBody] AddLocalUnitCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId, CommunityId = id }, ct);
        return CreatedAtAction(nameof(GetById), new { id }, result);
    }

    [HttpPatch("{id:guid}/parent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeParent(
        Guid id, [FromBody] ChangeCommunityParentCommand cmd, CancellationToken ct)
    {
        await sender.Send(cmd with { ActorId = ActorId, CommunityId = id }, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeactivateCommunityCommand(ActorId, id), ct);
        return NoContent();
    }
}
