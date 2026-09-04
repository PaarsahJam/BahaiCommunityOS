using Asp.Versioning;
using CommunityOS.Community.API.Extensions;
using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Community.API.Controllers;

/// <summary>
/// Self-scoped person resolution. Returns the Community Person linked to the
/// authenticated Identity account. The JWT <c>sub</c> claim is the sole input
/// — no caller-controlled identifier is accepted or trusted.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/my-person")]
[Authorize]
public sealed class MyPersonController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Returns the Community Person linked to the authenticated account.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PersonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var identityAccountId = User.GetSubjectId();
        var result = await sender.Send(new GetMyPersonQuery(identityAccountId), ct);
        return Ok(result);
    }
}
