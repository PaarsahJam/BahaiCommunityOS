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
[Route("api/v{version:apiVersion}/notifications")]
[Authorize]
public sealed class NotificationsController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet("inbox")]
    [ProducesResponseType<IReadOnlyList<NotificationSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListInbox(
        [FromQuery] string? type,
        [FromQuery] string? channel,
        [FromQuery] string? status,
        [FromQuery] Guid? organizationUnitId,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new ListInboxQuery(
            ActorId, type, channel, status, organizationUnitId, limit, offset), ct);
        return Ok(result);
    }

    [HttpGet("notifications/{id:guid}")]
    [ProducesResponseType<NotificationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetNotificationQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpGet("notifications/{id:guid}/sensitive")]
    [ProducesResponseType<NotificationSensitiveFieldsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSensitiveFields(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetNotificationSensitiveQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("notifications")]
    [ProducesResponseType<NotificationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(CreateNotificationRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateNotificationCommand(
            ActorId,
            request.TypeCode,
            request.Channel,
            request.SourceType,
            request.SourceId,
            request.OrganizationUnitId,
            request.AdditionalScopes,
            request.RecipientIds,
            request.ScheduledFor,
            request.IsSensitive,
            request.Subject,
            request.Body), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("notifications/{id:guid}/dispatch")]
    [ProducesResponseType<NotificationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Dispatch(Guid id, DispatchNotificationRequest? request, CancellationToken ct)
    {
        var result = await sender.Send(new DispatchNotificationCommand(
            ActorId, id, request?.AdminOverride ?? false), ct);
        return Ok(result);
    }

    [HttpPost("notifications/{id:guid}/recipients/{memberId:guid}/read")]
    [ProducesResponseType<NotificationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> MarkRead(Guid id, Guid memberId, CancellationToken ct)
    {
        var result = await sender.Send(new MarkNotificationReadCommand(ActorId, id, memberId), ct);
        return Ok(result);
    }
}

public sealed record CreateNotificationRequest(
    string TypeCode,
    string Channel,
    string? SourceType,
    Guid? SourceId,
    Guid? OrganizationUnitId,
    IReadOnlyList<Guid> AdditionalScopes,
    IReadOnlyList<Guid> RecipientIds,
    DateTime? ScheduledFor,
    bool IsSensitive,
    string Subject,
    string Body);

public sealed record DispatchNotificationRequest(bool AdminOverride = false);