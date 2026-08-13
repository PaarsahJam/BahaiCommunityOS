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
public sealed class DiscussionsController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet("questions/{questionId:guid}/discussions")]
    [ProducesResponseType<IReadOnlyList<DiscussionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid questionId, CancellationToken ct)
    {
        var result = await sender.Send(new ListDiscussionsByQuestionQuery(ActorId, questionId), ct);
        return Ok(result);
    }

    [HttpPost("questions/{questionId:guid}/discussions")]
    [ProducesResponseType<DiscussionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(Guid questionId, CreateDiscussionRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateDiscussionCommand(ActorId, questionId, request.Title), ct);
        return Ok(result);
    }

    [HttpPost("discussions/{discussionId:guid}/comments")]
    [ProducesResponseType<DiscussionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddComment(
        Guid discussionId, AddCommentRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new AddCommentCommand(
            ActorId, discussionId, request.Body, request.PassageIds), ct);
        return Ok(result);
    }

    [HttpPost("discussions/{discussionId:guid}/moderate")]
    [ProducesResponseType<DiscussionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Moderate(Guid discussionId, ModerateDiscussionRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new ModerateDiscussionCommand(ActorId, discussionId, request.Action), ct);
        return Ok(result);
    }
}

public sealed record CreateDiscussionRequest(string Title);

public sealed record AddCommentRequest(string Body, IReadOnlyList<Guid>? PassageIds);

public sealed record ModerateDiscussionRequest(string Action);