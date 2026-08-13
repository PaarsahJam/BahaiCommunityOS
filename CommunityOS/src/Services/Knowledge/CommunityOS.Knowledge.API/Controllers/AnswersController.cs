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
public sealed class AnswersController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet("questions/{questionId:guid}/answers")]
    [ProducesResponseType<IReadOnlyList<AnswerDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid questionId, CancellationToken ct)
    {
        var result = await sender.Send(new ListAnswersByQuestionQuery(ActorId, questionId), ct);
        return Ok(result);
    }

    [HttpGet("answers/{answerId:guid}")]
    [ProducesResponseType<AnswerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid answerId, CancellationToken ct)
    {
        var result = await sender.Send(new GetAnswerByIdQuery(ActorId, answerId), ct);
        return Ok(result);
    }

    [HttpPost("questions/{questionId:guid}/answers")]
    [ProducesResponseType<AnswerDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(Guid questionId, CreateAnswerRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateAnswerCommand(
            ActorId, questionId, request.Body, request.PassageIds), ct);
        return Ok(result);
    }

    [HttpPut("answers/{answerId:guid}")]
    [ProducesResponseType<AnswerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid answerId, UpdateAnswerRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateAnswerCommand(ActorId, answerId, request.Body), ct);
        return Ok(result);
    }

    [HttpPost("answers/{answerId:guid}/accept")]
    [ProducesResponseType<QuestionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Accept(
        Guid answerId, AcceptAnswerRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new AcceptAnswerCommand(ActorId, request.QuestionId, answerId), ct);
        return Ok(result);
    }
}

public sealed record CreateAnswerRequest(string Body, IReadOnlyList<Guid>? PassageIds);

public sealed record UpdateAnswerRequest(string Body);

public sealed record AcceptAnswerRequest(Guid QuestionId);