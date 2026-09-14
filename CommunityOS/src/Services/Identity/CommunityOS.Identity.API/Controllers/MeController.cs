using Asp.Versioning;
using CommunityOS.Identity.API.Extensions;
using CommunityOS.Identity.Application.Commands;
using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Identity.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/me")]
[ApiVersion(1.0)]
[Authorize]
public sealed class MeController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<UserAccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserAccountDto>> GetCurrent(CancellationToken ct)
        => Ok(await mediator.Send(new GetCurrentUserQuery(User.GetUserAccountId()), ct));

    [HttpPost("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request, CancellationToken ct)
    {
        await mediator.Send(new ChangePasswordCommand(
            User.GetUserAccountId(), request.CurrentPassword, request.NewPassword), ct);
        return NoContent();
    }

    [HttpGet("sessions")]
    [ProducesResponseType<IReadOnlyList<SessionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SessionDto>>> ListSessions(CancellationToken ct)
        => Ok(await mediator.Send(new ListSessionsQuery(
            User.GetUserAccountId(), User.GetSessionFamilyId()), ct));

    [HttpPost("sessions/{sessionId:guid}/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken ct)
    {
        await mediator.Send(new RevokeSessionCommand(
            User.GetUserAccountId(), sessionId), ct);
        return NoContent();
    }

    [HttpGet("security-events")]
    [ProducesResponseType<IReadOnlyList<SecurityEventDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SecurityEventDto>>> ListSecurityEvents(
        [FromQuery] int take = 100, CancellationToken ct = default)
        => Ok(await mediator.Send(
            new ListSecurityEventsQuery(User.GetUserAccountId(), take), ct));
}

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
