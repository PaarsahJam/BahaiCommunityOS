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
[Route("api/v{version:apiVersion}/localization/entity-translations")]
public sealed class EntityTranslationsController(IMediator mediator) : ControllerBase
{
    public sealed record QueryItemRequestBody(
        string SourceContext, string EntityType, Guid EntityId, string Field, string Culture);

    public sealed record QueryRequestBody(IReadOnlyList<QueryItemRequestBody> Refs, bool IncludePending);

    /// <summary>Batch resolution of by-reference display values (ADR-029
    /// decision 7). The owning service keeps its entities untouched.</summary>
    [HttpPost("query")]
    [ProducesResponseType<IReadOnlyList<EntityTranslationRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<EntityTranslationRow>>> Query(
        [FromBody] QueryRequestBody body,
        CancellationToken ct)
    {
        if (body?.Refs is null)
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        var rows = await mediator.Send(new QueryEntityTranslationsCommand(
            User.SubjectId(),
            body.Refs.Select(r => new EntityTranslationRef(
                r.SourceContext, r.EntityType, r.EntityId, r.Field, r.Culture)).ToList(),
            body.IncludePending), ct);
        return Ok(rows);
    }

    public sealed record UpsertItemRequestBody(
        string SourceContext, string EntityType, Guid EntityId, string Field,
        string Culture, string Value);

    public sealed record UpsertRequestBody(IReadOnlyList<UpsertItemRequestBody> Items);

    /// <summary>Each item becomes a NEW draft revision; approved values stay
    /// verbatim until review publishes the successor. Reserved source contexts
    /// (knowledge/library) are rejected with 409.</summary>
    [HttpPut("batch")]
    [ProducesResponseType<UpsertEntityTranslationsResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UpsertEntityTranslationsResult>> BatchUpsert(
        [FromBody] UpsertRequestBody body,
        CancellationToken ct)
    {
        if (body?.Items is null)
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        var result = await mediator.Send(new UpsertEntityTranslationsCommand(
            User.SubjectId(),
            body.Items.Select(i => new UpsertEntityTranslationItem(
                i.SourceContext, i.EntityType, i.EntityId, i.Field, i.Culture, i.Value)).ToList()), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType<TransitionStateResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TransitionStateResult>> Submit(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SubmitEntityTranslationCommand(User.SubjectId(), id), ct));

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType<TransitionStateResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TransitionStateResult>> Approve(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new ApproveEntityTranslationCommand(User.SubjectId(), id), ct));

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<TransitionStateResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TransitionStateResult>> Reject(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new RejectEntityTranslationCommand(User.SubjectId(), id), ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType<DeprecateEntityTranslationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeprecateEntityTranslationResult>> Deprecate(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new DeprecateEntityTranslationCommand(User.SubjectId(), id), ct));
}
