using Asp.Versioning;
using CommunityOS.Community.API.Extensions;
using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Community.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/calendar")]
[Authorize]
public sealed class CalendarController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CalendarEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? organizationUnitId,
        [FromQuery] string? types,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetCalendarQuery(ActorId, from, to, organizationUnitId, types), ct);
        return Ok(result);
    }
}