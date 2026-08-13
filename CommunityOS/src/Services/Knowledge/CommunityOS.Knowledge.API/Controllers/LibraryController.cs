using Asp.Versioning;
using CommunityOS.Knowledge.API.Config;
using CommunityOS.Knowledge.API.Extensions;
using CommunityOS.Knowledge.Application.Commands;
using CommunityOS.Knowledge.Application.DTOs;
using CommunityOS.Knowledge.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CommunityOS.Knowledge.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/library")]
[Authorize]
public sealed class LibraryController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet("works")]
    [ProducesResponseType<IReadOnlyList<WorkDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListWorks(
        [FromQuery] Guid? categoryId, [FromQuery] Guid? topicId, CancellationToken ct)
    {
        var result = await sender.Send(new ListWorksQuery(ActorId, categoryId, topicId), ct);
        return Ok(result);
    }

    [HttpGet("works/{id:guid}")]
    [ProducesResponseType<WorkDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWork(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetWorkByIdQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("works")]
    [ProducesResponseType<WorkDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateWork(
        CreateWorkRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateWorkCommand(
            ActorId, request.Title, request.WorkType, request.OriginalLanguage, request.DefaultLanguage), ct);
        return CreatedAtAction(nameof(GetWork), new { id = result.Id }, result);
    }

    [HttpPut("works/{id:guid}")]
    [ProducesResponseType<WorkDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateWork(
        Guid id, UpdateWorkRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateWorkCommand(
            ActorId, id, request.Title, request.WorkType, request.OriginalLanguage, request.DefaultLanguage), ct);
        return Ok(result);
    }

    [HttpGet("editions")]
    [ProducesResponseType<IReadOnlyList<EditionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListEditions(
        [FromQuery] Guid workId, [FromQuery] string? language, [FromQuery] bool? verified, CancellationToken ct)
    {
        var result = await sender.Send(new ListEditionsQuery(ActorId, workId, language, verified), ct);
        return Ok(result);
    }

    [HttpGet("editions/{id:guid}")]
    [ProducesResponseType<EditionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEdition(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetEditionByIdQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("editions")]
    [ProducesResponseType<EditionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportEdition(ImportEditionRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new ImportEditionCommand(
            ActorId, request.WorkId, request.Language, request.Translator, request.Publisher,
            request.EditionYear, request.Verified), ct);
        return CreatedAtAction(nameof(GetEdition), new { id = result.Id }, result);
    }

    [HttpPost("editions/{id:guid}/verify")]
    [ProducesResponseType<EditionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> VerifyEdition(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new VerifyEditionCommand(ActorId, id), ct);
        return Ok(result);
    }

    [HttpGet("passages")]
    [ProducesResponseType<IReadOnlyList<PassageDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPassages(
        [FromQuery] Guid editionId, [FromQuery] int? from, [FromQuery] int? to, CancellationToken ct)
    {
        var result = await sender.Send(new ListPassagesQuery(ActorId, editionId, from, to), ct);
        return Ok(result);
    }

    [HttpGet("passages/{id:guid}")]
    [ProducesResponseType<PassageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPassage(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetPassageByIdQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("passages")]
    [ProducesResponseType<PassageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportPassage(ImportPassageRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new ImportPassageCommand(
            ActorId, request.EditionId, request.ReferencePath, request.Text, request.SortOrder), ct);
        return CreatedAtAction(nameof(GetPassage), new { id = result.Id }, result);
    }

    [HttpPost("passages/{id:guid}/correct")]
    [ProducesResponseType<PassageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CorrectPassage(Guid id, CorrectPassageRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CorrectPassageCommand(ActorId, id, request.Text), ct);
        return Ok(result);
    }
}

public sealed record CreateWorkRequest(
    string Title,
    string WorkType,
    string OriginalLanguage,
    string DefaultLanguage);

public sealed record UpdateWorkRequest(
    string Title,
    string WorkType,
    string OriginalLanguage,
    string DefaultLanguage);

public sealed record ImportEditionRequest(
    Guid WorkId,
    string Language,
    string? Translator,
    string? Publisher,
    int? EditionYear,
    bool Verified);

public sealed record ImportPassageRequest(
    Guid EditionId,
    string ReferencePath,
    string Text,
    int SortOrder);

public sealed record CorrectPassageRequest(string Text);