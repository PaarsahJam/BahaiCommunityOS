using Asp.Versioning;
using CommunityOS.Records.API.Extensions;
using CommunityOS.Records.Application.Commands;
using CommunityOS.Records.Application.DTOs;
using CommunityOS.Records.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Records.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/categories")]
[Authorize]
public sealed class CategoriesController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RecordCategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await sender.Send(new ListRecordCategoriesQuery(ActorId), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<RecordCategoryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateRecordCategoryRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateRecordCategoryCommand(
            ActorId, request.Code, request.DisplayName, request.Description), ct);
        return CreatedAtAction(nameof(List), result);
    }

    [HttpPut("{code}")]
    [ProducesResponseType<RecordCategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string code, UpdateRecordCategoryRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateRecordCategoryCommand(
            ActorId, code, request.DisplayName, request.Description), ct);
        return Ok(result);
    }
}

public sealed record CreateRecordCategoryRequest(
    string Code,
    string DisplayName,
    string? Description);

public sealed record UpdateRecordCategoryRequest(
    string DisplayName,
    string? Description);