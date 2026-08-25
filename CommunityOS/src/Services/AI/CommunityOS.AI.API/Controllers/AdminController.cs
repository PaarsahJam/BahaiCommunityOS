using Asp.Versioning;
using CommunityOS.AI.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.AI.API.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ai/admin")]
public sealed class AdminController(IAiModelGateway gateway) : ControllerBase
{
    /// <summary>
    /// Lists configured AI providers.
    /// Requires ai.platform.manage permission (ADR-030 decision 12).
    /// At this gate, returns the disabled provider only.
    /// </summary>
    [HttpGet("providers")]
    [ProducesResponseType<IReadOnlyList<AiProviderInfo>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<IReadOnlyList<AiProviderInfo>> ListProviders() =>
        Ok(new[] { gateway.GetProviderInfo() });

    /// <summary>
    /// Lists available AI models.
    /// Requires ai.platform.manage permission (ADR-030 decision 12).
    /// At this gate, returns an empty list (no models configured).
    /// </summary>
    [HttpGet("models")]
    [ProducesResponseType<IReadOnlyList<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<IReadOnlyList<object>> ListModels() =>
        Ok(Array.Empty<object>());

    /// <summary>
    /// Lists prompt templates.
    /// Requires ai.platform.manage permission (ADR-030 decision 12).
    /// At this gate, returns an empty list (no templates configured).
    /// </summary>
    [HttpGet("templates")]
    [ProducesResponseType<IReadOnlyList<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<IReadOnlyList<object>> ListTemplates() =>
        Ok(Array.Empty<object>());

    /// <summary>
    /// Returns usage summaries.
    /// Requires ai.platform.manage permission (ADR-030 decision 12).
    /// At this gate, returns zeroed summary (no usage persistence).
    /// </summary>
    [HttpGet("usage")]
    [ProducesResponseType<UsageSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<UsageSummaryDto> GetUsage() =>
        Ok(new UsageSummaryDto { TotalRequests = 0, TotalTokens = 0 });

    public sealed record UsageSummaryDto
    {
        public long TotalRequests { get; init; }
        public long TotalTokens { get; init; }
    }
}
