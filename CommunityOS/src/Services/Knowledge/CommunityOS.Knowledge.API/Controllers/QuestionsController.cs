using Asp.Versioning;
using CommunityOS.Knowledge.API.Extensions;
using CommunityOS.Knowledge.Application.Commands;
using CommunityOS.Knowledge.Application.DTOs;
using CommunityOS.Knowledge.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Knowledge.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/questions")]
[Authorize]
public sealed class QuestionsController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<QuestionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? organizationUnitId,
        [FromQuery] string? query,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new ListQuestionsQuery(ActorId, status, categoryId, organizationUnitId, query), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<QuestionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetQuestionByIdQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<QuestionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateQuestionRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateQuestionCommand(
            ActorId,
            request.Title,
            request.Body,
            request.CategoryId,
            request.OrganizationUnitId,
            request.Tags), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType<QuestionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new SubmitQuestionCommand(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType<QuestionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new PublishQuestionCommand(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/flag")]
    [ProducesResponseType<QuestionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Flag(Guid id, FlagQuestionRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new FlagQuestionCommand(ActorId, id, request.Reason), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/under-review")]
    [ProducesResponseType<QuestionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnderReview(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new MoveUnderReviewCommand(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/merge-to/{targetId:guid}")]
    [ProducesResponseType<QuestionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MergeTo(Guid id, Guid targetId, CancellationToken ct)
    {
        var result = await sender.Send(new MergeQuestionCommand(ActorId, id, targetId), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType<QuestionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ArchiveQuestionCommand(ActorId, id), ct);
        return Ok(result);
    }
}

public sealed record CreateQuestionRequest(
    string Title,
    string Body,
    Guid? CategoryId,
    IReadOnlyList<string>? Tags,
    Guid? OrganizationUnitId);

public sealed record FlagQuestionRequest(string Reason);