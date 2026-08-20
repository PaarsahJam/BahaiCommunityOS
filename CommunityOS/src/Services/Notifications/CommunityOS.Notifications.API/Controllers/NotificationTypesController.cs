using Asp.Versioning;
using CommunityOS.Notifications.API.Extensions;
using CommunityOS.Notifications.Application.Commands;
using CommunityOS.Notifications.Application.DTOs;
using CommunityOS.Notifications.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Notifications.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications/types")]
[Authorize]
public sealed class NotificationTypesController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NotificationTypeDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await sender.Send(new ListNotificationTypesQuery(ActorId), ct);
        return Ok(result);
    }

    [HttpGet("{code}")]
    [ProducesResponseType<NotificationTypeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetByCode(string code, CancellationToken ct)
    {
        var result = await sender.Send(new GetNotificationTypeQuery(ActorId, code), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<NotificationTypeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(CreateNotificationTypeRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateNotificationTypeCommand(
            ActorId, request.Code, request.DisplayName, request.DefaultChannel,
            request.SubjectTemplate, request.BodyTemplate, request.IsSensitive), ct);
        return CreatedAtAction(nameof(GetByCode), new { code = result.Code }, result);
    }

    [HttpPut("{code}")]
    [ProducesResponseType<NotificationTypeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(string code, UpdateNotificationTypeRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateNotificationTypeCommand(
            ActorId, code, request.DisplayName, request.DefaultChannel,
            request.SubjectTemplate, request.BodyTemplate, request.IsSensitive), ct);
        return Ok(result);
    }

    [HttpPost("{code}/retire")]
    [ProducesResponseType<NotificationTypeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Retire(string code, CancellationToken ct)
    {
        var result = await sender.Send(new RetireNotificationTypeCommand(ActorId, code), ct);
        return Ok(result);
    }
}

public sealed record CreateNotificationTypeRequest(
    string Code,
    string DisplayName,
    string DefaultChannel,
    string SubjectTemplate,
    string BodyTemplate,
    bool IsSensitive);

public sealed record UpdateNotificationTypeRequest(
    string DisplayName,
    string DefaultChannel,
    string SubjectTemplate,
    string BodyTemplate,
    bool IsSensitive);