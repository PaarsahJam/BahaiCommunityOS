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
[Route("api/v{version:apiVersion}/localization/resources/entries")]
public sealed class ResourceEntriesController(IMediator mediator) : ControllerBase
{
    /// <summary>Deterministic keyset walk. List responses are metadata-only:
    /// revision values are never included (ADR-029 decision 10). The response
    /// carries <c>nextCursor</c>; it is null on the final page.</summary>
    [HttpGet]
    [ProducesResponseType<WalkEntriesResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<WalkEntriesResult>> Walk(
        [FromQuery] Guid? namespaceId,
        [FromQuery] string? state,
        [FromQuery] string? search,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var page = await mediator.Send(new WalkEntriesQuery(
            User.SubjectId(), namespaceId, state, search, cursor,
            limit ?? 25), ct);
        return Ok(page);
    }

    public sealed record CreateEntryRequestBody(string Key, string Culture, string Value);

    [HttpPost]
    [ProducesResponseType<ResourceEntryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResourceEntryDto>> Create(
        [FromQuery] Guid namespaceId,
        [FromBody] CreateEntryRequestBody body,
        CancellationToken ct)
    {
        if (body is null)
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        var dto = await mediator.Send(new CreateResourceEntryCommand(
            User.SubjectId(), namespaceId, body.Key, body.Culture, body.Value), ct);
        return CreatedAtAction(nameof(Walk), dto);
    }

    public sealed record AddRevisionRequestBody(string Culture, string Value);

    /// <summary>Editing is proposing a NEW draft; the approved value stays
    /// verbatim until review publishes the successor (ADR-029 decision 4).</summary>
    [HttpPost("{id:guid}/revisions")]
    [ProducesResponseType<AddRevisionResult>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AddRevisionResult>> AddRevision(
        Guid id,
        [FromBody] AddRevisionRequestBody body,
        CancellationToken ct)
    {
        if (body is null)
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        var result = await mediator.Send(new AddResourceRevisionCommand(
            User.SubjectId(), id, body.Culture, body.Value), ct);
        return CreatedAtAction(nameof(Walk), result);
    }

    [HttpPost("{id:guid}/revisions/{revisionId:guid}/submit")]
    [ProducesResponseType<TransitionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TransitionResult>> Submit(
        Guid id, Guid revisionId, CancellationToken ct) =>
        Ok(await mediator.Send(new SubmitRevisionCommand(User.SubjectId(), id, revisionId), ct));

    /// <summary>The publishing act — requires
    /// <c>localization.resource.review</c>; emits LocalizationCatalogChanged
    /// through the outbox atomically.</summary>
    [HttpPost("{id:guid}/revisions/{revisionId:guid}/approve")]
    [ProducesResponseType<TransitionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TransitionResult>> Approve(
        Guid id, Guid revisionId, CancellationToken ct) =>
        Ok(await mediator.Send(new ApproveRevisionCommand(User.SubjectId(), id, revisionId), ct));

    [HttpPost("{id:guid}/revisions/{revisionId:guid}/reject")]
    [ProducesResponseType<TransitionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TransitionResult>> Reject(
        Guid id, Guid revisionId, CancellationToken ct) =>
        Ok(await mediator.Send(new RejectRevisionCommand(User.SubjectId(), id, revisionId), ct));

    /// <summary>Soft deprecation only — nothing is ever hard-deleted
    /// (ADR-029 decision 9).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType<DeprecateEntryResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeprecateEntryResult>> Deprecate(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new DeprecateResourceEntryCommand(User.SubjectId(), id), ct));
}
