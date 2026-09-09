using Asp.Versioning;
using CommunityOS.Identity.API.Extensions;
using CommunityOS.Identity.Application.Commands;
using CommunityOS.Identity.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Identity.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/mfa")]
[ApiVersion(1.0)]
[Authorize]
public sealed class MfaController(IMediator mediator) : ControllerBase
{
    [HttpPost("enroll")]
    [ProducesResponseType<MfaEnrollmentDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MfaEnrollmentDto>> BeginEnrollment(CancellationToken ct)
        => Ok(await mediator.Send(
            new BeginMfaEnrollmentCommand(User.GetUserAccountId()), ct));

    [HttpPost("enroll/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteEnrollment(
        CompleteMfaEnrollmentRequest request, CancellationToken ct)
    {
        await mediator.Send(new CompleteMfaEnrollmentCommand(
            User.GetUserAccountId(), request.MfaMethodId, request.Code), ct);
        return NoContent();
    }

    [HttpDelete("{methodId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveMfa(Guid methodId, CancellationToken ct)
    {
        await mediator.Send(new RemoveMfaCommand(User.GetUserAccountId(), methodId), ct);
        return NoContent();
    }
}

public sealed record CompleteMfaEnrollmentRequest(Guid MfaMethodId, string Code);
