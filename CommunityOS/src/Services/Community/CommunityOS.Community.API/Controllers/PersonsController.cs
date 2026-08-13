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
[Route("api/v{version:apiVersion}/persons")]
[Authorize]
public sealed class PersonsController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PersonDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await sender.Send(new GetPersonsQuery(ActorId), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PersonDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetPersonByIdQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<PersonDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePersonCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId }, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}/profile")]
    [ProducesResponseType<PersonDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(
        Guid id, [FromBody] UpdatePersonProfileCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId, PersonId = id }, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}/contact-methods")]
    [ProducesResponseType<PersonDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetContactMethods(
        Guid id, [FromBody] SetPersonContactMethodsCommand cmd, CancellationToken ct)
    {
        var result = await sender.Send(cmd with { ActorId = ActorId, PersonId = id }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/identity-link")]
    [ProducesResponseType<PersonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LinkIdentity(
        Guid id, [FromBody] IdentityLinkRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new LinkPersonToIdentityAccountCommand(ActorId, id, request.IdentityAccountId), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/identity-unlink")]
    [ProducesResponseType<PersonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkIdentity(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new UnlinkPersonFromIdentityAccountCommand(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<PersonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeactivatePersonCommand(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/reactivate")]
    [ProducesResponseType<PersonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ReactivatePersonCommand(ActorId, id), ct);
        return Ok(result);
    }
}

public sealed record IdentityLinkRequest(Guid IdentityAccountId);