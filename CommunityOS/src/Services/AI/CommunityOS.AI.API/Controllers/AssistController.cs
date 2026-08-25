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
[Route("api/v{version:apiVersion}/ai/assist")]
public sealed class AssistController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Invokes AI assistance for a specific capability.
    /// Requires ai.assist.invoke permission (ADR-030 decision 12).
    /// At this gate, always returns 503 — provider is disabled.
    /// </summary>
    [HttpPost("{capability}")]
    [ProducesResponseType<AssistInvocationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AssistInvocationResult>> Invoke(
        string capability,
        [FromBody] AssistInvocationRequestBody body,
        CancellationToken ct)
    {
        if (body is null)
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        var result = await mediator.Send(
            new AssistInvocationCommand
            {
                SubjectId = User.SubjectId(),
                Capability = capability,
                Input = body.Input
            }, ct);

        if (result.Outcome == "provider_disabled")
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }

        if (result.Outcome == "capability_unsupported")
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    public sealed record AssistInvocationRequestBody(string Input);
}
