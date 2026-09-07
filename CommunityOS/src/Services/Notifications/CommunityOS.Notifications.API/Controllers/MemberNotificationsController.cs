using Asp.Versioning;
using CommunityOS.Notifications.API.Extensions;
using CommunityOS.Notifications.Application.Commands;
using CommunityOS.Notifications.Application.DTOs;
using CommunityOS.Notifications.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Notifications.API.Controllers;

/// <summary>
/// Member-safe read contract (ADR-027). Distinct first route segment
/// (<c>my-notifications</c>) so the public API Gateway can expose exactly this
/// surface without exposing administrative notification endpoints. Recipient
/// scope is always the authenticated subject — never a route or query-supplied
/// <c>memberId</c>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/my-notifications")]
[Authorize]
public sealed class MemberNotificationsController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MemberNotificationSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> List(
        [FromQuery] int limit = ListMemberNotificationsQuery.DefaultPageSize,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new ListMemberNotificationsQuery(ActorId, limit, offset), ct);
        return Ok(result);
    }

    [HttpGet("unread-count")]
    [ProducesResponseType<MemberUnreadCountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        var result = await sender.Send(new GetMemberUnreadCountQuery(ActorId), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType<MemberNotificationSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new MarkMemberNotificationReadCommand(ActorId, id), ct);
        return Ok(result);
    }
}