using Asp.Versioning;
using CommunityOS.Localization.API.Extensions;
using CommunityOS.Localization.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Localization.API.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/localization/resources/namespaces")]
public sealed class NamespacesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NamespaceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<NamespaceDto>>> List(CancellationToken ct) =>
        Ok(await mediator.Send(new ListNamespaces(User.SubjectId()), ct));

    public sealed record CreateNamespaceRequestBody(string Name, string? Description);

    /// <summary>Reserved prefixes (the Knowledge/Library boundary) are
    /// rejected with 409 (ADR-029 decision 18).</summary>
    [HttpPost]
    [ProducesResponseType<NamespaceDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<NamespaceDto>> Create(
        [FromBody] CreateNamespaceRequestBody body,
        CancellationToken ct)
    {
        if (body is null)
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        var dto = await mediator.Send(
            new CreateNamespaceCommand(User.SubjectId(), body.Name, body.Description), ct);
        return CreatedAtAction(nameof(List), dto);
    }
}
