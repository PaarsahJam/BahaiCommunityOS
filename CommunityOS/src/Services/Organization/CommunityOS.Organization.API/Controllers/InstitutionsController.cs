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
[Route("api/v{version:apiVersion}/institutions")]
[Authorize]
public sealed class InstitutionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<InstitutionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<InstitutionDto>>> List(CancellationToken ct)
    {
        var result = await sender.Send(new GetInstitutionsQuery(User.GetSubjectId()), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<InstitutionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InstitutionDto>> Create(
        CreateInstitutionRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateInstitutionCommand(
            User.GetSubjectId(),
            request.Name,
            request.InstitutionType,
            request.JurisdictionType,
            request.JurisdictionScopeId,
            request.EstablishedOn), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<InstitutionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InstitutionDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetInstitutionByIdQuery(User.GetSubjectId(), id), ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<InstitutionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InstitutionDto>> Update(
        Guid id, UpdateInstitutionRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateInstitutionCommand(
            User.GetSubjectId(), id, request.Name, request.JurisdictionType, request.JurisdictionScopeId), ct);
        return Ok(result);
    }
}

public sealed record CreateInstitutionRequest(
    string Name,
    string InstitutionType,
    string JurisdictionType,
    Guid? JurisdictionScopeId,
    DateTime? EstablishedOn);

public sealed record UpdateInstitutionRequest(
    string Name,
    string JurisdictionType,
    Guid? JurisdictionScopeId);
