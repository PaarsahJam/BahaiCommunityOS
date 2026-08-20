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
[Route("api/v{version:apiVersion}/notifications/preferences")]
[Authorize]
public sealed class PreferencesController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet("me")]
    [ProducesResponseType<MemberNotificationPreferencesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var result = await sender.Send(new GetMemberPreferencesQuery(ActorId), ct);
        return Ok(result);
    }

    [HttpPut("me")]
    [ProducesResponseType<MemberNotificationPreferencesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateMine(UpdatePreferencesRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateMemberPreferencesCommand(ActorId, request.Rules), ct);
        return Ok(result);
    }
}

public sealed record UpdatePreferencesRequest(IReadOnlyList<NotificationPreferenceRuleDto> Rules);