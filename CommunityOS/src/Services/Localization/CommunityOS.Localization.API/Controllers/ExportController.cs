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
[Route("api/v{version:apiVersion}/localization/export/bundles")]
public sealed class ExportController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Resolves one culture's export bundle with BCP-47 fallback chain and
    /// default-locale fallback (ADR-029 decision 3). Unresolved keys are
    /// omitted — clients render the key identifier itself (fail-visible).
    /// The response carries a deterministic ETag derived from the global
    /// catalog version (ADR-029 decisions 14/17).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ExportBundleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ExportBundleDto>> GetBundle(
        [FromQuery] string culture,
        [FromQuery] Guid? @namespace,
        CancellationToken ct)
    {
        var bundle = await mediator.Send(
            new ExportBundleQuery(User.SubjectId(), @namespace, culture), ct);
        Response.Headers.ETag = bundle.ETag;
        return Ok(bundle);
    }
}
