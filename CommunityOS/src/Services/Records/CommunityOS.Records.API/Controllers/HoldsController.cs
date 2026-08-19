using Asp.Versioning;
using CommunityOS.Records.API.Extensions;
using CommunityOS.Records.Application.Commands;
using CommunityOS.Records.Application.DTOs;
using CommunityOS.Records.Application.Queries;
using CommunityOS.Records.Domain.Aggregates;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Records.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/holds")]
[Authorize]
public sealed class HoldsController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RecordHoldDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? recordId,
        [FromQuery] string? holdType,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var result = await sender.Send(new ListHoldsQuery(ActorId, recordId, holdType, status), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RecordHoldDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetHoldQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<RecordHoldDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Place(PlaceHoldRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new PlaceHoldCommand(
            ActorId,
            request.RecordId,
            request.HoldType,
            request.Reason,
            request.DocumentReferences?
                .Select(r => RecordHoldDocumentReference.Create(r.DocumentId, r.VersionNumber))
                .ToList()), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/release")]
    [ProducesResponseType<RecordHoldDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Release(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ReleaseHoldCommand(ActorId, id), ct);
        return Ok(result);
    }
}

public sealed record RecordHoldDocumentReferenceRequest(Guid DocumentId, int? VersionNumber);

public sealed record PlaceHoldRequest(
    Guid RecordId,
    string HoldType,
    string Reason,
    IReadOnlyList<RecordHoldDocumentReferenceRequest>? DocumentReferences);