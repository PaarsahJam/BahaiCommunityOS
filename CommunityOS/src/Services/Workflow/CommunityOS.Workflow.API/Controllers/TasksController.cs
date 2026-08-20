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
[Route("api/v{version:apiVersion}/workflow/tasks")]
[Authorize]
public sealed class TasksController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<WorkflowTaskSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? definitionCode,
        [FromQuery] string? status,
        [FromQuery] Guid? assigneeId,
        [FromQuery] string? domainType,
        [FromQuery] Guid? domainEntityId,
        [FromQuery] Guid? organizationUnitId,
        [FromQuery] bool? overdue,
        CancellationToken ct)
    {
        var result = await sender.Send(new ListWorkflowTasksQuery(
            ActorId, definitionCode, status, assigneeId, domainType, domainEntityId, organizationUnitId, overdue), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<WorkflowTaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetWorkflowTaskQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/sensitive")]
    [ProducesResponseType<WorkflowTaskSensitiveFieldsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSensitiveFields(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetWorkflowTaskSensitiveFieldsQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<WorkflowTaskDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateWorkflowTaskRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateWorkflowTaskCommand(
            ActorId,
            request.DefinitionCode,
            request.DomainType,
            request.DomainEntityId,
            request.OrganizationUnitId,
            request.AdditionalScopes,
            request.AssigneeIds,
            request.DueOn,
            request.Notes), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/assign")]
    [ProducesResponseType<WorkflowTaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Assign(Guid id, AssignWorkflowTaskRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new AssignWorkflowTaskCommand(ActorId, id, request.AssigneeIds), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/start")]
    [ProducesResponseType<WorkflowTaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(Guid id, StartWorkflowTaskRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new StartWorkflowTaskCommand(ActorId, id, request.AdminOverride), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType<WorkflowTaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(Guid id, CompleteWorkflowTaskRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CompleteWorkflowTaskCommand(
            ActorId, id, request.Outcome, request.Notes, request.AdminOverride), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<WorkflowTaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new CancelWorkflowTaskCommand(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/escalate")]
    [ProducesResponseType<WorkflowTaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Escalate(Guid id, EscalateWorkflowTaskRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new EscalateWorkflowTaskCommand(ActorId, id, request.EscalateTo, request.Reason), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/activity")]
    [ProducesResponseType<IReadOnlyList<TaskActivityDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListActivity(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ListWorkflowTaskActivityQuery(ActorId, id), ct);
        return Ok(result);
    }
}

public sealed record CreateWorkflowTaskRequest(
    string DefinitionCode,
    string DomainType,
    Guid? DomainEntityId,
    Guid? OrganizationUnitId,
    IReadOnlyList<Guid> AdditionalScopes,
    IReadOnlyList<Guid> AssigneeIds,
    DateTime? DueOn,
    string? Notes);

public sealed record AssignWorkflowTaskRequest(IReadOnlyList<Guid> AssigneeIds);

public sealed record StartWorkflowTaskRequest(bool AdminOverride);

public sealed record CompleteWorkflowTaskRequest(string Outcome, string? Notes, bool AdminOverride);

public sealed record EscalateWorkflowTaskRequest(Guid EscalateTo, string Reason);