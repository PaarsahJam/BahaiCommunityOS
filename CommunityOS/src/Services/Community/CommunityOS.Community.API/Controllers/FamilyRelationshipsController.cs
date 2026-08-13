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
[Route("api/v{version:apiVersion}/family-relationships")]
[Authorize]
public sealed class FamilyRelationshipsController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<FamilyRelationshipDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByPerson(
        [FromQuery] Guid personId, CancellationToken ct)
    {
        var result = await sender.Send(new GetFamilyRelationshipsQuery(ActorId, personId), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<FamilyRelationshipDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateFamilyRelationshipCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId }, ct);
        return CreatedAtAction(nameof(ListByPerson), new { personId = result.PersonIdA }, result);
    }

    [HttpPost("{id:guid}/end")]
    [ProducesResponseType<FamilyRelationshipDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> End(
        Guid id, [FromBody] EndFamilyRelationshipRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new EndFamilyRelationshipCommand(ActorId, id, request.EndedOn), ct);
        return Ok(result);
    }
}

public sealed record EndFamilyRelationshipRequest(DateTime EndedOn);