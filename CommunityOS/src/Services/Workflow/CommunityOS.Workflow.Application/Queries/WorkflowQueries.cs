using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Workflow.Application.Authorization;
using CommunityOS.Workflow.Application.DTOs;
using CommunityOS.Workflow.Application.Permissions;
using CommunityOS.Workflow.Domain.Aggregates;
using CommunityOS.Workflow.Domain.Enumerations;
using CommunityOS.Workflow.Domain.Exceptions;
using CommunityOS.Workflow.Domain.Repositories;
using MediatR;

namespace CommunityOS.Workflow.Application.Queries;

public sealed record ListWorkflowTasksQuery(
    Guid ActorId,
    string? DefinitionCode,
    string? Status,
    Guid? AssigneeId,
    string? DomainType,
    Guid? DomainEntityId,
    Guid? OrganizationUnitId,
    bool? Overdue) : IRequest<IReadOnlyList<WorkflowTaskSummaryDto>>;

internal sealed class ListWorkflowTasksQueryHandler(
    IWorkflowTaskRepository tasks,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator)
    : IRequestHandler<ListWorkflowTasksQuery, IReadOnlyList<WorkflowTaskSummaryDto>>
{
    public async Task<IReadOnlyList<WorkflowTaskSummaryDto>> Handle(
        ListWorkflowTasksQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, WorkflowPermissions.TaskRead,
            query.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "workflow.task")
                : new AuthorizationContext(ResourceType: "workflow.task"), ct);

        var asOf = DateTime.UtcNow;
        var candidates = query.OrganizationUnitId is { } filterUnit
            ? await tasks.ListByOrganizationUnitAsync(filterUnit, ct)
            : await tasks.ListAsync(ct);

        var filtered = candidates
            .Where(t => query.DefinitionCode is null ||
                string.Equals(t.DefinitionCode, query.DefinitionCode, StringComparison.OrdinalIgnoreCase))
            .Where(t => query.Status is null ||
                string.Equals(t.Status.Name, query.Status, StringComparison.OrdinalIgnoreCase))
            .Where(t => query.DomainType is null ||
                string.Equals(t.DomainType, query.DomainType, StringComparison.OrdinalIgnoreCase))
            .Where(t => query.DomainEntityId is null || t.DomainEntityId == query.DomainEntityId)
            .Where(t => query.AssigneeId is null || t.IsAssignee(query.AssigneeId.Value))
            .Where(t => query.Overdue is null || t.IsOverdue(asOf) == query.Overdue)
            .ToList();

        // Fail-closed read filtering at the query boundary: only tasks the
        // caller may read are returned; nothing reveals the existence or count
        // of unauthorized tasks (ADR-024). A task is readable when the caller
        // holds the read permission at ANY of its organization scopes.
        var requests = new List<AuthorizationRequest>();
        var slices = new List<(WorkflowTask Task, int Count)>();
        foreach (var t in filtered)
        {
            var contexts = WorkflowTaskAuthorization.ContextsFor(t);
            foreach (var context in contexts)
                requests.Add(new AuthorizationRequest(query.ActorId, WorkflowPermissions.TaskRead, context));
            slices.Add((t, contexts.Count));
        }

        var decisions = await evaluator.EvaluateBatchAsync(requests, ct);

        var allowed = new List<WorkflowTask>();
        var index = 0;
        foreach (var (task, count) in slices)
        {
            var slice = decisions.Skip(index).Take(count);
            index += count;
            if (slice.Any(d => d.Allowed))
                allowed.Add(task);
        }

        return allowed
            .OrderByDescending(t => t.UpdatedOn)
            .Select(t => t.ToSummaryDto(asOf))
            .ToList()
            .AsReadOnly();
    }
}

public sealed record GetWorkflowTaskQuery(Guid ActorId, Guid TaskId) : IRequest<WorkflowTaskDto>;

internal sealed class GetWorkflowTaskQueryHandler(
    IWorkflowTaskRepository tasks,
    AuthorizationGuard guard)
    : IRequestHandler<GetWorkflowTaskQuery, WorkflowTaskDto>
{
    public async Task<WorkflowTaskDto> Handle(GetWorkflowTaskQuery query, CancellationToken ct)
    {
        var task = await LoadOrNothingAsync(tasks, query.TaskId, ct);

        if (!await WorkflowTaskAuthorization.HasForTaskAsync(
                guard, query.ActorId, WorkflowPermissions.TaskRead, task, ct))
            throw new WorkflowTaskNotFoundException(query.TaskId);

        return task.ToDto(DateTime.UtcNow);
    }

    internal static async Task<WorkflowTask> LoadOrNothingAsync(
        IWorkflowTaskRepository tasks, Guid taskId, CancellationToken ct) =>
        await tasks.GetByIdAsync(taskId, ct)
        ?? throw new WorkflowTaskNotFoundException(taskId);
}

public sealed record GetWorkflowTaskSensitiveFieldsQuery(Guid ActorId, Guid TaskId)
    : IRequest<WorkflowTaskSensitiveFieldsDto>;

internal sealed class GetWorkflowTaskSensitiveFieldsQueryHandler(
    IWorkflowTaskRepository tasks,
    AuthorizationGuard guard)
    : IRequestHandler<GetWorkflowTaskSensitiveFieldsQuery, WorkflowTaskSensitiveFieldsDto>
{
    public async Task<WorkflowTaskSensitiveFieldsDto> Handle(
        GetWorkflowTaskSensitiveFieldsQuery query, CancellationToken ct)
    {
        var task = await GetWorkflowTaskQueryHandler.LoadOrNothingAsync(tasks, query.TaskId, ct);

        // Sensitive reads require both the base read permission and the
        // sensitive capability; any failure surfaces as 404 (no oracle).
        if (!await WorkflowTaskAuthorization.HasForTaskAsync(
                guard, query.ActorId, WorkflowPermissions.TaskRead, task, ct) ||
            !await WorkflowTaskAuthorization.HasForTaskAsync(
                guard, query.ActorId, WorkflowPermissions.TaskReadSensitive, task, ct))
            throw new WorkflowTaskNotFoundException(query.TaskId);

        return task.ToSensitiveFieldsDto();
    }
}

public sealed record ListWorkflowTaskActivityQuery(Guid ActorId, Guid TaskId)
    : IRequest<IReadOnlyList<TaskActivityDto>>;

internal sealed class ListWorkflowTaskActivityQueryHandler(
    IWorkflowTaskRepository tasks,
    AuthorizationGuard guard)
    : IRequestHandler<ListWorkflowTaskActivityQuery, IReadOnlyList<TaskActivityDto>>
{
    public async Task<IReadOnlyList<TaskActivityDto>> Handle(
        ListWorkflowTaskActivityQuery query, CancellationToken ct)
    {
        var task = await GetWorkflowTaskQueryHandler.LoadOrNothingAsync(tasks, query.TaskId, ct);

        if (!await WorkflowTaskAuthorization.HasForTaskAsync(
                guard, query.ActorId, WorkflowPermissions.TaskRead, task, ct))
            throw new WorkflowTaskNotFoundException(query.TaskId);

        // Activity notes are sensitive and excluded from activity DTOs.
        return task.Activity
            .OrderBy(a => a.OccurredOn)
            .Select(a => a.ToDto(task.Id))
            .ToList()
            .AsReadOnly();
    }
}

public sealed record ListTaskDefinitionsQuery(Guid ActorId)
    : IRequest<IReadOnlyList<TaskDefinitionDto>>;

internal sealed class ListTaskDefinitionsQueryHandler(
    ITaskDefinitionRepository definitions,
    AuthorizationGuard guard)
    : IRequestHandler<ListTaskDefinitionsQuery, IReadOnlyList<TaskDefinitionDto>>
{
    public async Task<IReadOnlyList<TaskDefinitionDto>> Handle(
        ListTaskDefinitionsQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, WorkflowPermissions.DefinitionRead,
            new AuthorizationContext(ResourceType: "workflow.task"), ct);

        return (await definitions.ListAsync(ct))
            .OrderBy(d => d.Code)
            .Select(d => d.ToDto())
            .ToList()
            .AsReadOnly();
    }
}

public sealed record GetTaskDefinitionQuery(Guid ActorId, string Code)
    : IRequest<TaskDefinitionDto>;

internal sealed class GetTaskDefinitionQueryHandler(
    ITaskDefinitionRepository definitions,
    AuthorizationGuard guard)
    : IRequestHandler<GetTaskDefinitionQuery, TaskDefinitionDto>
{
    public async Task<TaskDefinitionDto> Handle(GetTaskDefinitionQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, WorkflowPermissions.DefinitionRead,
            new AuthorizationContext(ResourceType: "workflow.task"), ct);

        var definition = await definitions.GetByCodeAsync(query.Code, ct)
            ?? throw new TaskDefinitionNotFoundException(query.Code);

        return definition.ToDto();
    }
}