using Asp.Versioning;
using CommunityOS.Organization.API.Extensions;
using CommunityOS.Organization.Application.Commands;
using CommunityOS.Organization.Application.DTOs;
using CommunityOS.Organization.Application.Queries;
using CommunityOS.Organization.Domain.Enumerations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Organization.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/appointments")]
[Authorize]
public sealed class AppointmentsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AppointmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<AppointmentDto>>> List(
        [FromQuery] Guid? personId,
        [FromQuery] Guid? organizationUnitId,
        [FromQuery] DateTime? asOf,
        [FromQuery] AppointmentView? view,
        CancellationToken ct)
    {
        var result = await sender.Send(new GetAppointmentsQuery(
            User.GetSubjectId(), personId, organizationUnitId, asOf, view), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentDto>> Assign(
        AssignAppointmentRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new AssignAppointmentCommand(
            User.GetSubjectId(),
            request.PersonId,
            request.OrganizationUnitId,
            request.AppointmentType,
            request.EffectiveFrom,
            request.EffectiveUntil,
            request.Reason), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetAppointmentByIdQuery(User.GetSubjectId(), id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/end")]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentDto>> End(
        Guid id, EndAppointmentRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new EndAppointmentCommand(
            User.GetSubjectId(), id, request.Reason), ct);
        return Ok(result);
    }
}

public sealed record AssignAppointmentRequest(
    Guid PersonId,
    Guid OrganizationUnitId,
    string AppointmentType,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil,
    string? Reason);

public sealed record EndAppointmentRequest(string? Reason);
