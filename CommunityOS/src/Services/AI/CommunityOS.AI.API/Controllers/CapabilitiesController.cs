using Asp.Versioning;
using CommunityOS.AI.API.Extensions;
using CommunityOS.AI.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.AI.API.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ai/capabilities")]
public sealed class CapabilitiesController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Lists available AI capabilities.
    /// Requires ai.assist.invoke permission (ADR-030 decision 12).
    /// At this gate, returns an empty list (no provider enabled).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ListCapabilitiesResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ListCapabilitiesResult>> List(CancellationToken ct) =>
        Ok(await mediator.Send(new ListCapabilitiesQuery { SubjectId = User.SubjectId() }, ct));
}
