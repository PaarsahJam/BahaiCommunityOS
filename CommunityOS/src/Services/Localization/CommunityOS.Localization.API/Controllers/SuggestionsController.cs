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
[Route("api/v{version:apiVersion}/localization/suggestions")]
public sealed class SuggestionsController(IMediator mediator) : ControllerBase
{
    /// <summary>Curation surface; reading suggestions requires
    /// <c>localization.resource.review</c> (ADR-029 decision 19).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SuggestionRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<SuggestionRow>>> List(
        [FromQuery] string? status,
        [FromQuery] int? limit,
        CancellationToken ct) =>
        Ok(await mediator.Send(
            new ListSuggestionsQuery(User.SubjectId(), status, limit ?? 25), ct));

    /// <summary>Acceptance moves the content into the ordinary human review
    /// workflow as InReview — it can never publish directly (ADR-029
    /// decision 19). Requires propose.</summary>
    [HttpPost("{id:guid}/accept-into-review")]
    [ProducesResponseType<SuggestionDecisionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SuggestionDecisionResult>> Accept(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new AcceptSuggestionIntoReviewCommand(User.SubjectId(), id), ct));

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<SuggestionDecisionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SuggestionDecisionResult>> Reject(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new RejectSuggestionCommand(User.SubjectId(), id), ct));
}
