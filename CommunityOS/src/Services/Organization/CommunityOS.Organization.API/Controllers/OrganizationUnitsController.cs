using Asp.Versioning;
using CommunityOS.Organization.API.Config;
using CommunityOS.Organization.API.Extensions;
using CommunityOS.Organization.Application.Commands;
using CommunityOS.Organization.Application.DTOs;
using CommunityOS.Organization.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CommunityOS.Organization.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/orgunits")]
[Authorize]
public sealed class OrganizationUnitsController(
    ISender sender,
    IOptions<OrganizationApiOptions> options) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrganizationUnitDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<OrganizationUnitDto>>> List(
        [FromQuery] Guid organizationId, [FromQuery] DateTime? asOf, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetOrganizationUnitsQuery(User.GetSubjectId(), organizationId, asOf), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<OrganizationUnitDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrganizationUnitDto>> Create(
        CreateOrganizationUnitRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateOrganizationUnitCommand(
            User.GetSubjectId(),
            request.OrganizationId,
            request.Name,
            request.UnitType,
            request.ParentId,
            request.EffectiveFrom), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrganizationUnitDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationUnitDetailDto>> GetById(
        Guid id, [FromQuery] DateTime? asOf, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetOrganizationUnitByIdQuery(User.GetSubjectId(), id, asOf), ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType<OrganizationUnitDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationUnitDto>> Update(
        Guid id, UpdateOrganizationUnitRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateOrganizationUnitCommand(
            User.GetSubjectId(), id, request.Name, request.UnitType), ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/parent")]
    [ProducesResponseType<OrganizationUnitDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrganizationUnitDto>> ChangeParent(
        Guid id, ChangeOrganizationUnitParentRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new ChangeOrganizationUnitParentCommand(
            User.GetSubjectId(), id, request.ParentId, request.EffectiveFrom, request.EffectiveUntil), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<OrganizationUnitDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrganizationUnitDto>> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(
            new DeactivateOrganizationUnitCommand(User.GetSubjectId(), id), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/children")]
    [ProducesResponseType<IReadOnlyList<OrganizationUnitDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrganizationUnitDto>>> Children(
        Guid id, [FromQuery] DateTime? asOf, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetOrganizationUnitChildrenQuery(User.GetSubjectId(), id, asOf), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/ancestors")]
    [ProducesResponseType<IReadOnlyList<OrganizationUnitDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrganizationUnitDto>>> Ancestors(
        Guid id, [FromQuery] DateTime? asOf, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetOrganizationUnitAncestorsQuery(User.GetSubjectId(), id, asOf), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/descendants")]
    [ProducesResponseType<IReadOnlyList<OrganizationUnitDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrganizationUnitDto>>> Descendants(
        Guid id, [FromQuery] DateTime? asOf, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetOrganizationUnitDescendantsQuery(User.GetSubjectId(), id, asOf), ct);
        return Ok(result);
    }

    /// <summary>
    /// Narrow internal fact endpoint for the Authorization service's
    /// organization context provider. Answers whether <c>ancestorId</c> is the
    /// same unit as, or an ancestor of, this unit at <c>asOf</c>. It is
    /// service-only (verified via the <c>X-Client-Id</c> header) and performs
    /// no application-layer permission check: routing it through the permission
    /// guard would create a request cycle with the Authorization service.
    /// Fail closed — unknown clients are rejected.
    /// </summary>
    [HttpGet("{id:guid}/covers")]
    [ProducesResponseType<OrganizationUnitCoverageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrganizationUnitCoverageDto>> Covers(
        Guid id,
        [FromQuery] Guid ancestorId,
        [FromQuery] DateTime? asOf,
        CancellationToken ct)
    {
        if (!string.Equals(
                Request.Headers["X-Client-Id"],
                options.Value.InternalClientId,
                StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var result = await sender.Send(
            new GetOrganizationUnitCoveredQuery(id, ancestorId, asOf), ct);
        return Ok(new OrganizationUnitCoverageDto(id, ancestorId, result, DateTime.UtcNow));
    }
}

public sealed record CreateOrganizationUnitRequest(
    Guid OrganizationId,
    string Name,
    string UnitType,
    Guid? ParentId,
    DateTime EffectiveFrom);

public sealed record UpdateOrganizationUnitRequest(string Name, string UnitType);

public sealed record ChangeOrganizationUnitParentRequest(
    Guid? ParentId,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil);
