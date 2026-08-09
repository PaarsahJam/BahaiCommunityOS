using Asp.Versioning;
using CommunityOS.Identity.API.Extensions;
using CommunityOS.Identity.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Identity.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/account")]
[ApiVersion(1.0)]
[Authorize]
public sealed class AccountController(IMediator mediator) : ControllerBase
{
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        LogoutRequest request, CancellationToken ct)
    {
        await mediator.Send(new LogoutCommand(request.RefreshToken), ct);
        return NoContent();
    }

    [HttpPost("external-identities")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LinkExternalIdentity(
        ExternalIdentityRequest request, CancellationToken ct)
    {
        await mediator.Send(new LinkExternalIdentityCommand(
            User.GetUserAccountId(), request.Provider, request.Subject), ct);
        return NoContent();
    }

    [HttpDelete("external-identities")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UnlinkExternalIdentity(
        ExternalIdentityRequest request, CancellationToken ct)
    {
        await mediator.Send(new UnlinkExternalIdentityCommand(
            User.GetUserAccountId(), request.Provider, request.Subject), ct);
        return NoContent();
    }
}

public sealed record LogoutRequest(string RefreshToken);

public sealed record ExternalIdentityRequest(string Provider, string Subject);
