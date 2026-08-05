using Asp.Versioning;
using CommunityOS.Identity.Application.Commands;
using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Identity.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/members")]
[Authorize]
public sealed class MembersController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<MemberDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetMemberByIdQuery(id), ct);
        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MemberDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByLocalUnit(
        [FromQuery] Guid localUnitId, CancellationToken ct)
    {
        var result = await sender.Send(new GetMembersByLocalUnitQuery(localUnitId), ct);
        return Ok(result);
    }

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType<MemberDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterMemberCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}/contact")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateContact(
        Guid id, [FromBody] UpdateMemberContactCommand cmd, CancellationToken ct)
    {
        await sender.Send(cmd with { MemberId = id }, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await sender.Send(new ActivateMemberCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/suspend")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Suspend(
        Guid id, [FromBody] SuspendMemberCommand cmd, CancellationToken ct)
    {
        await sender.Send(cmd with { MemberId = id }, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/transfer")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Transfer(
        Guid id, [FromBody] TransferMemberCommand cmd, CancellationToken ct)
    {
        await sender.Send(cmd with { MemberId = id }, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/roles")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRole(
        Guid id, [FromBody] AssignRoleCommand cmd, CancellationToken ct)
    {
        await sender.Send(cmd with { MemberId = id }, ct);
        return NoContent();
    }
}
