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
[Route("api/v{version:apiVersion}/memberships")]
[Authorize]
public sealed class MembershipsController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MembershipDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await sender.Send(new GetMembershipsQuery(ActorId), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<MembershipDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetMembershipByIdQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpGet("by-person/{personId:guid}")]
    [ProducesResponseType<MembershipDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByPerson(Guid personId, CancellationToken ct)
    {
        var result = await sender.Send(new GetMembershipByPersonQuery(ActorId, personId), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<MembershipDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateMembershipCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId }, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<MembershipDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(
        Guid id, [FromBody] ChangeMembershipStatusCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId, MembershipId = id }, ct);
        return Ok(result);
    }
}