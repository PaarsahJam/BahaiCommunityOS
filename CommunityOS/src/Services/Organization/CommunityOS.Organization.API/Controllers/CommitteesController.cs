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
[Route("api/v{version:apiVersion}/committees")]
[Authorize]
public sealed class CommitteesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CommitteeDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CommitteeDto>>> List(
        [FromQuery] Guid? organizationId, [FromQuery] DateTime? asOf, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetCommitteesQuery(User.GetSubjectId(), organizationId, asOf), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<CommitteeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CommitteeDto>> Create(
        CreateCommitteeRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateCommitteeCommand(
            User.GetSubjectId(),
            request.Name,
            request.CommitteeType,
            request.OrganizationId,
            request.OrganizationUnitId,
            request.JurisdictionType,
            request.JurisdictionScopeId), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CommitteeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommitteeDto>> GetById(
        Guid id, [FromQuery] DateTime? asOf, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetCommitteeByIdQuery(User.GetSubjectId(), id, asOf), ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<CommitteeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommitteeDto>> Update(
        Guid id, UpdateCommitteeRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateCommitteeCommand(
            User.GetSubjectId(), id, request.Name, request.JurisdictionType, request.JurisdictionScopeId), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/members")]
    [ProducesResponseType<CommitteeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommitteeDto>> AddMember(
        Guid id, AddCommitteeMemberRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new AddCommitteeMemberCommand(
            User.GetSubjectId(),
            id,
            request.PersonId,
            request.RoleCode,
            request.EffectiveFrom,
            request.EffectiveUntil), ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/members/{personId:guid}")]
    [ProducesResponseType<CommitteeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommitteeDto>> RemoveMember(
        Guid id, Guid personId, [FromQuery] string roleCode, CancellationToken ct)
    {
        var result = await sender.Send(new RemoveCommitteeMemberCommand(
            User.GetSubjectId(), id, personId, roleCode), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<CommitteeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommitteeDto>> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(
            new DeactivateCommitteeCommand(User.GetSubjectId(), id), ct);
        return Ok(result);
    }
}

public sealed record CreateCommitteeRequest(
    string Name,
    string CommitteeType,
    Guid OrganizationId,
    Guid? OrganizationUnitId,
    string JurisdictionType,
    Guid? JurisdictionScopeId);

public sealed record UpdateCommitteeRequest(
    string Name,
    string JurisdictionType,
    Guid? JurisdictionScopeId);

public sealed record AddCommitteeMemberRequest(
    Guid PersonId,
    string RoleCode,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil);
