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
[Route("api/v{version:apiVersion}")]
[Authorize]
public sealed class AiSuggestionsController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet("questions/{questionId:guid}/ai-suggestions")]
    [ProducesResponseType<IReadOnlyList<AiSuggestionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid questionId, CancellationToken ct)
    {
        var result = await sender.Send(new ListAiSuggestionsByQuestionQuery(ActorId, questionId), ct);
        return Ok(result);
    }

    [HttpPost("questions/{questionId:guid}/ai-suggestions/request")]
    [ProducesResponseType<AiSuggestionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestSuggestion(Guid questionId, RequestAiSuggestionRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new RequestAiSuggestionCommand(
            ActorId, questionId, request.ModelId, request.Instruction, request.PassageIds), ct);
        return Ok(result);
    }

    [HttpPost("ai-suggestions/{suggestionId:guid}/accept")]
    [ProducesResponseType<AiSuggestionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Accept(Guid suggestionId, CancellationToken ct)
    {
        var result = await sender.Send(new AcceptAiSuggestionCommand(ActorId, suggestionId), ct);
        return Ok(result);
    }

    [HttpPost("ai-suggestions/{suggestionId:guid}/reject")]
    [ProducesResponseType<AiSuggestionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(Guid suggestionId, CancellationToken ct)
    {
        var result = await sender.Send(new RejectAiSuggestionCommand(ActorId, suggestionId), ct);
        return Ok(result);
    }
}

public sealed record RequestAiSuggestionRequest(
    string ModelId,
    string Instruction,
    IReadOnlyList<Guid>? PassageIds);