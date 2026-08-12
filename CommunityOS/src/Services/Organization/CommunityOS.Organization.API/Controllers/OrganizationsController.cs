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
[Route("api/v{version:apiVersion}/organizations")]
[Authorize]
public sealed class OrganizationsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrganizationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<OrganizationDto>>> List(CancellationToken ct)
    {
        var result = await sender.Send(new GetOrganizationsQuery(User.GetSubjectId()), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<OrganizationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrganizationDto>> Create(
        CreateOrganizationRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateOrganizationCommand(
            User.GetSubjectId(),
            request.Name,
            request.OrganizationType,
            request.JurisdictionType,
            request.JurisdictionScopeId,
            request.EstablishedOn), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrganizationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetOrganizationByIdQuery(User.GetSubjectId(), id), ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<OrganizationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDto>> Update(
        Guid id, UpdateOrganizationRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateOrganizationCommand(
            User.GetSubjectId(),
            id,
            request.Name,
            request.JurisdictionType,
            request.JurisdictionScopeId), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/dissolve")]
    [ProducesResponseType<OrganizationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDto>> Dissolve(
        Guid id, DissolveOrganizationRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new DissolveOrganizationCommand(
            User.GetSubjectId(), id, request.DissolvedOn), ct);
        return Ok(result);
    }
}

public sealed record CreateOrganizationRequest(
    string Name,
    string OrganizationType,
    string JurisdictionType,
    Guid? JurisdictionScopeId,
    DateTime? EstablishedOn);

public sealed record UpdateOrganizationRequest(
    string Name,
    string JurisdictionType,
    Guid? JurisdictionScopeId);

public sealed record DissolveOrganizationRequest(DateTime DissolvedOn);
