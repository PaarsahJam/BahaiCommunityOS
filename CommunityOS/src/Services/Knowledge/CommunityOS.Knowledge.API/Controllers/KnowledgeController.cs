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
[Route("api/v{version:apiVersion}")]
[Authorize]
public sealed class KnowledgeController(
    ISender sender,
    IOptions<KnowledgeApiOptions> options) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet("categories")]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCategories(CancellationToken ct)
    {
        var result = await sender.Send(new ListCategoriesQuery(ActorId), ct);
        return Ok(result);
    }

    [HttpPost("categories")]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCategory(CreateCategoryRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateCategoryCommand(ActorId, request.Name, request.Description), ct);
        return Ok(result);
    }

    [HttpPut("categories/{id:guid}")]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategory(Guid id, UpdateCategoryRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateCategoryCommand(ActorId, id, request.Name, request.Description), ct);
        return Ok(result);
    }

    [HttpGet("topics")]
    [ProducesResponseType<IReadOnlyList<TopicDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTopics(CancellationToken ct)
    {
        var result = await sender.Send(new ListTopicsQuery(ActorId), ct);
        return Ok(result);
    }

    [HttpPost("topics")]
    [ProducesResponseType<TopicDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTopic(CreateTopicRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateTopicCommand(ActorId, request.Name, request.Description), ct);
        return Ok(result);
    }

    [HttpPut("topics/{id:guid}")]
    [ProducesResponseType<TopicDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTopic(Guid id, UpdateTopicRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateTopicCommand(ActorId, id, request.Name, request.Description), ct);
        return Ok(result);
    }

    /// <summary>
    /// Narrow internal fact endpoint used by other services (e.g. the
    /// Authorization service organization context provider) to resolve the
    /// authoritative citation text for a passage. It is service-only (verified
    /// via the <c>X-Client-Id</c> header) and performs no application-layer
    /// permission check: routing it through the permission guard would create
    /// a request cycle with the Authorization service. Fail closed — unknown
    /// clients are rejected.
    /// </summary>
    [HttpGet("library/passages/{id:guid}/citation")]
    [ProducesResponseType<CitationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResolveCitation(Guid id, CancellationToken ct)
    {
        if (!string.Equals(
                Request.Headers["X-Client-Id"],
                options.Value.InternalClientId,
                StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var result = await sender.Send(new ResolveCitationQuery(ActorId, id), ct);
        return Ok(result);
    }
}

public sealed record CreateCategoryRequest(string Name, string? Description);

public sealed record UpdateCategoryRequest(string Name, string? Description);

public sealed record CreateTopicRequest(string Name, string? Description);

public sealed record UpdateTopicRequest(string Name, string? Description);