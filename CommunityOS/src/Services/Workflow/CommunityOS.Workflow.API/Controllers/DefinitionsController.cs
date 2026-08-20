using Asp.Versioning;
using CommunityOS.Workflow.API.Extensions;
using CommunityOS.Workflow.Application.Commands;
using CommunityOS.Workflow.Application.DTOs;
using CommunityOS.Workflow.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Workflow.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/workflow/definitions")]
[Authorize]
public sealed class DefinitionsController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TaskDefinitionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await sender.Send(new ListTaskDefinitionsQuery(ActorId), ct);
        return Ok(result);
    }

    [HttpGet("{code}")]
    [ProducesResponseType<TaskDefinitionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByCode(string code, CancellationToken ct)
    {
        var result = await sender.Send(new GetTaskDefinitionQuery(ActorId, code), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<TaskDefinitionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateTaskDefinitionRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateTaskDefinitionCommand(
            ActorId, request.Code, request.DisplayName, request.Description, request.DomainType,
            request.PermittedOutcomes, request.DueIn, request.RequiresHumanReview), ct);
        return CreatedAtAction(nameof(GetByCode), new { code = result.Code }, result);
    }

    [HttpPut("{code}")]
    [ProducesResponseType<TaskDefinitionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(string code, UpdateTaskDefinitionRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateTaskDefinitionCommand(
            ActorId, code, request.DisplayName, request.Description, request.DomainType,
            request.PermittedOutcomes, request.DueIn, request.RequiresHumanReview), ct);
        return Ok(result);
    }

    [HttpPost("{code}/retire")]
    [ProducesResponseType<TaskDefinitionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Retire(string code, CancellationToken ct)
    {
        var result = await sender.Send(new RetireTaskDefinitionCommand(ActorId, code), ct);
        return Ok(result);
    }
}

public sealed record CreateTaskDefinitionRequest(
    string Code,
    string DisplayName,
    string Description,
    string DomainType,
    IReadOnlyList<string> PermittedOutcomes,
    string? DueIn,
    bool RequiresHumanReview);

public sealed record UpdateTaskDefinitionRequest(
    string DisplayName,
    string Description,
    string DomainType,
    IReadOnlyList<string> PermittedOutcomes,
    string? DueIn,
    bool RequiresHumanReview);