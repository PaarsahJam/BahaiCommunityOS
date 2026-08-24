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
[Route("api/v{version:apiVersion}/localization/locales")]
public sealed class LocalesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<LocaleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<LocaleDto>>> List(CancellationToken ct) =>
        Ok(await mediator.Send(new ListLocales(User.SubjectId()), ct));

    public sealed record CreateLocaleRequestBody(string Code, string? DisplayName);

    /// <summary>Registers a culture; registration alone resolves nothing —
    /// activation is explicit (ADR-029 decision 3).</summary>
    [HttpPost]
    [ProducesResponseType<CreatedLocaleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedLocaleDto>> Create(
        [FromBody] CreateLocaleRequestBody body,
        CancellationToken ct)
    {
        if (body is null)
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        var dto = await mediator.Send(
            new CreateLocaleCommand(User.SubjectId(), body.Code, body.DisplayName), ct);
        return CreatedAtAction(nameof(List), dto);
    }

    [HttpPost("{code}/activate")]
    [ProducesResponseType<ChangeLocaleStatusResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ChangeLocaleStatusResult>> Activate(string code, CancellationToken ct) =>
        Ok(await mediator.Send(new ActivateLocaleCommand(User.SubjectId(), code), ct));

    /// <summary>The default locale cannot be deactivated — 409 otherwise.</summary>
    [HttpPost("{code}/deactivate")]
    [ProducesResponseType<ChangeLocaleStatusResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ChangeLocaleStatusResult>> Deactivate(string code, CancellationToken ct) =>
        Ok(await mediator.Send(new DeactivateLocaleCommand(User.SubjectId(), code), ct));
}
