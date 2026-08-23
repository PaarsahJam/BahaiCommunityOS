using Asp.Versioning;
using CommunityOS.Correspondence.API.Extensions;
using CommunityOS.Correspondence.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Correspondence.API.Controllers;

/// <summary>
/// Administrative operations (correspondence.letter.admin): materialization
/// reconciliation, legal/administrative holds and the guarded retention purge.
/// </summary>
[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/correspondence/admin")]
public sealed class AdminController(IMediator mediator) : ControllerBase
{
    [HttpPost("reconcile-materializations")]
    [ProducesResponseType<ReconcileMaterializationsResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ReconcileMaterializationsResult>> ReconcileMaterializations(CancellationToken ct) =>
        Ok(await mediator.Send(new ReconcileMaterializationsCommand(User.SubjectId()), ct));

    public sealed record PlaceHoldsRequestBody(IReadOnlyList<Guid> LetterIds, string HoldType, string ReasonCode);

    [HttpPost("holds")]
    [ProducesResponseType<IReadOnlyList<HoldRow>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyList<HoldRow>>> PlaceHolds(
        [FromBody] PlaceHoldsRequestBody body,
        CancellationToken ct)
    {
        if (body is null || body.LetterIds is null || body.LetterIds.Count == 0)
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        var holds = await mediator.Send(new PlaceHoldsCommand(
            User.SubjectId(), body.LetterIds, body.HoldType, body.ReasonCode), ct);
        return CreatedAtAction(nameof(PlaceHolds), holds);
    }

    [HttpPost("holds/{holdId:guid}/release")]
    [ProducesResponseType<HoldRow>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HoldRow>> ReleaseHold(Guid holdId, CancellationToken ct) =>
        Ok(await mediator.Send(new ReleaseHoldCommand(User.SubjectId(), holdId), ct));

    public sealed record PurgeExpiredRequestBody(int MaxBatchSize = 0);

    [HttpPost("purge-expired")]
    [ProducesResponseType<PurgeExpiredResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PurgeExpiredResult>> PurgeExpired(
        [FromBody] PurgeExpiredRequestBody body,
        CancellationToken ct)
    {
        if (body is null)
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        return Ok(await mediator.Send(new PurgeExpiredLettersCommand(
            User.SubjectId(), body.MaxBatchSize), ct));
    }
}
