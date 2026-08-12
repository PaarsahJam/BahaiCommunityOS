using Asp.Versioning;
using CommunityOS.Organization.API.Extensions;
using CommunityOS.Organization.Application.Commands;
using CommunityOS.Organization.Application.DTOs;
using CommunityOS.Organization.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Organization.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/delegations")]
[Authorize]
public sealed class DelegationFactsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DelegationFactDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<DelegationFactDto>>> List(
        [FromQuery] Guid? delegatorId,
        [FromQuery] Guid? delegateId,
        [FromQuery] Guid? organizationUnitId,
        CancellationToken ct)
    {
        var result = await sender.Send(new GetDelegationFactsQuery(
            User.GetSubjectId(), delegatorId, delegateId, organizationUnitId), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<DelegationFactDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DelegationFactDto>> Grant(
        GrantDelegationFactRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new GrantDelegationFactCommand(
            User.GetSubjectId(),
            request.DelegatorId,
            request.DelegateId,
            request.OrganizationUnitId,
            request.DelegationType,
            request.EffectiveFrom,
            request.EffectiveUntil,
            request.Reason), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DelegationFactDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DelegationFactDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetDelegationFactByIdQuery(User.GetSubjectId(), id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/revoke")]
    [ProducesResponseType<DelegationFactDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DelegationFactDto>> Revoke(
        Guid id, RevokeDelegationFactRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new RevokeDelegationFactCommand(
            User.GetSubjectId(), id, request.Reason), ct);
        return Ok(result);
    }
}

public sealed record GrantDelegationFactRequest(
    Guid DelegatorId,
    Guid DelegateId,
    Guid OrganizationUnitId,
    string DelegationType,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil,
    string? Reason);

public sealed record RevokeDelegationFactRequest(string? Reason);
