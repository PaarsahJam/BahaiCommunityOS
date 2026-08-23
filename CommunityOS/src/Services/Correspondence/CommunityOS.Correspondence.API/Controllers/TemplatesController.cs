using Asp.Versioning;
using CommunityOS.Correspondence.API.Extensions;
using CommunityOS.Correspondence.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Correspondence.API.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/correspondence/templates")]
public sealed class TemplatesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TemplateDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<TemplateDto>>> List(CancellationToken ct) =>
        Ok(await mediator.Send(new ListTemplates(User.SubjectId()), ct));

    public sealed record CreateTemplateRequestBody(
        string Code, string Title, string SubjectTemplate, string BodyTemplate, string Category);

    [HttpPost]
    [ProducesResponseType<TemplateDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TemplateDto>> Create(
        [FromBody] CreateTemplateRequestBody body,
        CancellationToken ct)
    {
        if (body is null)
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        var dto = await mediator.Send(new CreateTemplateCommand(
            User.SubjectId(), body.Code, body.Title,
            body.SubjectTemplate, body.BodyTemplate, body.Category), ct);
        return CreatedAtAction(nameof(List), dto);
    }

    public sealed record UpdateTemplateRequestBody(
        string? Title, string? SubjectTemplate, string? BodyTemplate,
        string? Category, bool? IsActive);

    [HttpPut("{id:guid}")]
    [ProducesResponseType<TemplateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TemplateDto>> Update(
        Guid id,
        [FromBody] UpdateTemplateRequestBody requestBody,
        CancellationToken ct)
    {
        if (requestBody is null)
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        return Ok(await mediator.Send(new UpdateTemplateCommand(
            User.SubjectId(), id, requestBody.Title, requestBody.SubjectTemplate,
            requestBody.BodyTemplate, requestBody.Category, requestBody.IsActive), ct));
    }

    /// <summary>Soft deactivation: the template stays for audit and existing
    /// letters but disappears from the active list and can no longer seed new
    /// drafts.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeactivateTemplateCommand(User.SubjectId(), id), ct);
        return NoContent();
    }
}
