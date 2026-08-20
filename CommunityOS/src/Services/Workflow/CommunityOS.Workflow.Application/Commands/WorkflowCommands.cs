using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Workflow.Application.Authorization;
using CommunityOS.Workflow.Application.DTOs;
using CommunityOS.Workflow.Application.Logging;
using CommunityOS.Workflow.Application.Permissions;
using CommunityOS.Workflow.Application.Pipeline;
using CommunityOS.Workflow.Domain.Aggregates;
using CommunityOS.Workflow.Domain.Exceptions;
using CommunityOS.Workflow.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using static CommunityOS.Workflow.Application.Commands.WorkflowCommandHelpers;
using WorkflowEventsPublisher = CommunityOS.Workflow.Application.Pipeline.DomainEventPublisher;

namespace CommunityOS.Workflow.Application.Commands;

public sealed record CreateWorkflowTaskCommand(
    Guid ActorId,
    string DefinitionCode,
    string DomainType,
    Guid? DomainEntityId,
    Guid? OrganizationUnitId,
    IReadOnlyList<Guid> AdditionalScopes,
    IReadOnlyList<Guid> AssigneeIds,
    DateTime? DueOn,
    string? Notes) : IRequest<WorkflowTaskDto>;

internal sealed class CreateWorkflowTaskCommandHandler(
    IWorkflowTaskRepository tasks,
    ITaskDefinitionRepository definitions,
    IOrganizationUnitReferenceRepository units,
    AuthorizationGuard guard,
    IMediator mediator,
    ILogger<CreateWorkflowTaskCommandHandler> logger)
    : IRequestHandler<CreateWorkflowTaskCommand, WorkflowTaskDto>
{
    public async Task<WorkflowTaskDto> Handle(CreateWorkflowTaskCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, WorkflowPermissions.TaskCreate,
            cmd.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "workflow.task")
                : new AuthorizationContext(ResourceType: "workflow.task"), ct);

        var definition = await definitions.GetByCodeAsync(cmd.DefinitionCode, ct)
            ?? throw new InvalidTaskDefinitionReferenceException(cmd.DefinitionCode);

        await EnsureUnitExistsAsync(units, cmd.OrganizationUnitId, ct);
        foreach (var scope in cmd.AdditionalScopes ?? [])
            await EnsureUnitExistsAsync(units, scope, ct);

        // Creation is idempotent per (definition, domain entity): a second
        // create for an already-open domain-bound task returns the existing
        // task (docs/api/workflow.md). Free-standing general tasks are always
        // new.
        var domainBound =
            !string.Equals(cmd.DomainType, CommunityOS.Workflow.Domain.Enumerations.WorkflowDomainTypes.General,
                StringComparison.OrdinalIgnoreCase);

        if (domainBound && cmd.DomainEntityId is { } domainEntityId)
        {
            var existing = await tasks.FindOpenAsync(
                cmd.DefinitionCode, cmd.DomainType, domainEntityId, ct);
            if (existing is not null)
                return existing.ToDto(DateTime.UtcNow);
        }

        var task = WorkflowTask.Create(
            cmd.DefinitionCode,
            cmd.DomainType,
            cmd.DomainEntityId,
            cmd.OrganizationUnitId,
            cmd.AdditionalScopes ?? [],
            cmd.AssigneeIds ?? [],
            cmd.DueOn,
            cmd.Notes,
            originatorId: cmd.ActorId,
            cmd.ActorId,
            DateTime.UtcNow);

        // Outbox: publish the created domain event before the single
        // transaction commits so the forwarded integration event and the task
        // row are committed atomically (ADR-015, ratified).
        await WorkflowEventsPublisher.PublishAsync(task, mediator, ct);
        var persisted = await tasks.AddIfAbsentAsync(task, ct);

        if (persisted.Id != task.Id)
        {
            // A concurrent create won the race; the unique filtered index
            // rejected our insert and the existing open task was returned.
            logger.TaskCreated(persisted.Id, persisted.DefinitionCode);
            return persisted.ToDto(DateTime.UtcNow);
        }

        logger.TaskCreated(task.Id, task.DefinitionCode);
        return task.ToDto(DateTime.UtcNow);
    }
}

public sealed record AssignWorkflowTaskCommand(
    Guid ActorId,
    Guid TaskId,
    IReadOnlyList<Guid> AssigneeIds) : IRequest<WorkflowTaskDto>;

internal sealed class AssignWorkflowTaskCommandHandler(
    IWorkflowTaskRepository tasks,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<AssignWorkflowTaskCommand, WorkflowTaskDto>
{
    public async Task<WorkflowTaskDto> Handle(AssignWorkflowTaskCommand cmd, CancellationToken ct)
    {
        var task = await LoadForMutationAsync(tasks, cmd.TaskId, ct);
        await WorkflowTaskAuthorization.RequireForTaskAsync(guard, cmd.ActorId, WorkflowPermissions.TaskAssign, task, ct);

        task.Assign(cmd.AssigneeIds, cmd.ActorId, DateTime.UtcNow);
        await SaveAsync(task, tasks, mediator, ct);

        return task.ToDto(DateTime.UtcNow);
    }
}

public sealed record StartWorkflowTaskCommand(
    Guid ActorId,
    Guid TaskId,
    bool AdminOverride = false) : IRequest<WorkflowTaskDto>;

internal sealed class StartWorkflowTaskCommandHandler(
    IWorkflowTaskRepository tasks,
    AuthorizationGuard guard,
    IMediator mediator,
    ILogger<StartWorkflowTaskCommandHandler> logger)
    : IRequestHandler<StartWorkflowTaskCommand, WorkflowTaskDto>
{
    public async Task<WorkflowTaskDto> Handle(StartWorkflowTaskCommand cmd, CancellationToken ct)
    {
        var task = await LoadForMutationAsync(tasks, cmd.TaskId, ct);
        await WorkflowTaskAuthorization.RequireForTaskAsync(guard, cmd.ActorId, WorkflowPermissions.TaskStart, task, ct);

        if (cmd.AdminOverride)
        {
            await WorkflowTaskAuthorization.RequireForTaskAsync(
                guard, cmd.ActorId, WorkflowPermissions.TaskAdmin, task, ct);
            logger.AdminOverrideApplied(task.Id, "start");
        }

        task.Start(cmd.ActorId, cmd.AdminOverride, DateTime.UtcNow);
        await SaveAsync(task, tasks, mediator, ct);

        return task.ToDto(DateTime.UtcNow);
    }
}

public sealed record CompleteWorkflowTaskCommand(
    Guid ActorId,
    Guid TaskId,
    string Outcome,
    string? Notes,
    bool AdminOverride = false) : IRequest<WorkflowTaskDto>;

internal sealed class CompleteWorkflowTaskCommandHandler(
    IWorkflowTaskRepository tasks,
    ITaskDefinitionRepository definitions,
    AuthorizationGuard guard,
    IMediator mediator,
    ILogger<CompleteWorkflowTaskCommandHandler> logger)
    : IRequestHandler<CompleteWorkflowTaskCommand, WorkflowTaskDto>
{
    public async Task<WorkflowTaskDto> Handle(CompleteWorkflowTaskCommand cmd, CancellationToken ct)
    {
        var task = await LoadForMutationAsync(tasks, cmd.TaskId, ct);
        await WorkflowTaskAuthorization.RequireForTaskAsync(guard, cmd.ActorId, WorkflowPermissions.TaskComplete, task, ct);

        if (cmd.AdminOverride)
        {
            await WorkflowTaskAuthorization.RequireForTaskAsync(
                guard, cmd.ActorId, WorkflowPermissions.TaskAdmin, task, ct);
            logger.AdminOverrideApplied(task.Id, "complete");
        }

        var definition = await definitions.GetByCodeAsync(task.DefinitionCode, ct)
            ?? throw new InvalidTaskDefinitionReferenceException(task.DefinitionCode);

        task.Complete(
            cmd.Outcome, cmd.Notes, definition.PermittedOutcomeCodes, cmd.ActorId, cmd.AdminOverride, DateTime.UtcNow);
        await SaveAsync(task, tasks, mediator, ct);

        return task.ToDto(DateTime.UtcNow);
    }
}

public sealed record CancelWorkflowTaskCommand(Guid ActorId, Guid TaskId) : IRequest<WorkflowTaskDto>;

internal sealed class CancelWorkflowTaskCommandHandler(
    IWorkflowTaskRepository tasks,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<CancelWorkflowTaskCommand, WorkflowTaskDto>
{
    public async Task<WorkflowTaskDto> Handle(CancelWorkflowTaskCommand cmd, CancellationToken ct)
    {
        var task = await LoadForMutationAsync(tasks, cmd.TaskId, ct);
        await WorkflowTaskAuthorization.RequireForTaskAsync(guard, cmd.ActorId, WorkflowPermissions.TaskCancel, task, ct);

        task.Cancel(cmd.ActorId, DateTime.UtcNow);
        await SaveAsync(task, tasks, mediator, ct);

        return task.ToDto(DateTime.UtcNow);
    }
}

public sealed record EscalateWorkflowTaskCommand(
    Guid ActorId,
    Guid TaskId,
    Guid EscalateTo,
    string Reason) : IRequest<WorkflowTaskDto>;

internal sealed class EscalateWorkflowTaskCommandHandler(
    IWorkflowTaskRepository tasks,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<EscalateWorkflowTaskCommand, WorkflowTaskDto>
{
    public async Task<WorkflowTaskDto> Handle(EscalateWorkflowTaskCommand cmd, CancellationToken ct)
    {
        var task = await LoadForMutationAsync(tasks, cmd.TaskId, ct);
        await WorkflowTaskAuthorization.RequireForTaskAsync(guard, cmd.ActorId, WorkflowPermissions.TaskEscalate, task, ct);

        task.Escalate(cmd.EscalateTo, cmd.Reason, cmd.ActorId, DateTime.UtcNow);
        await SaveAsync(task, tasks, mediator, ct);

        return task.ToDto(DateTime.UtcNow);
    }
}

public sealed record CreateTaskDefinitionCommand(
    Guid ActorId,
    string Code,
    string DisplayName,
    string Description,
    string DomainType,
    IReadOnlyList<string> PermittedOutcomes,
    string? DueIn,
    bool RequiresHumanReview) : IRequest<TaskDefinitionDto>;

internal sealed class CreateTaskDefinitionCommandHandler(
    ITaskDefinitionRepository definitions,
    AuthorizationGuard guard,
    ILogger<CreateTaskDefinitionCommandHandler> logger)
    : IRequestHandler<CreateTaskDefinitionCommand, TaskDefinitionDto>
{
    public async Task<TaskDefinitionDto> Handle(CreateTaskDefinitionCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, WorkflowPermissions.DefinitionManage,
            new AuthorizationContext(ResourceType: "workflow.task"), ct);

        if (await definitions.ExistsByCodeAsync(cmd.Code, ct))
            throw new DuplicateTaskDefinitionException(cmd.Code);

        var definition = TaskDefinition.Create(
            cmd.Code, cmd.DisplayName, cmd.Description, cmd.DomainType,
            cmd.PermittedOutcomes, cmd.DueIn, cmd.RequiresHumanReview, cmd.ActorId, DateTime.UtcNow);

        await definitions.AddAsync(definition, ct);
        logger.TaskDefinitionCreated(definition.Code);
        return definition.ToDto();
    }
}

public sealed record UpdateTaskDefinitionCommand(
    Guid ActorId,
    string Code,
    string DisplayName,
    string Description,
    string DomainType,
    IReadOnlyList<string> PermittedOutcomes,
    string? DueIn,
    bool RequiresHumanReview) : IRequest<TaskDefinitionDto>;

internal sealed class UpdateTaskDefinitionCommandHandler(
    ITaskDefinitionRepository definitions,
    AuthorizationGuard guard)
    : IRequestHandler<UpdateTaskDefinitionCommand, TaskDefinitionDto>
{
    public async Task<TaskDefinitionDto> Handle(UpdateTaskDefinitionCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, WorkflowPermissions.DefinitionManage,
            new AuthorizationContext(ResourceType: "workflow.task"), ct);

        var definition = await definitions.GetByCodeAsync(cmd.Code, ct)
            ?? throw new TaskDefinitionNotFoundException(cmd.Code);

        definition.Update(
            cmd.DisplayName, cmd.Description, cmd.DomainType,
            cmd.PermittedOutcomes, cmd.DueIn, cmd.RequiresHumanReview, cmd.ActorId, DateTime.UtcNow);

        await definitions.UpdateAsync(definition, ct);
        return definition.ToDto();
    }
}

public sealed record RetireTaskDefinitionCommand(Guid ActorId, string Code)
    : IRequest<TaskDefinitionDto>;

internal sealed class RetireTaskDefinitionCommandHandler(
    ITaskDefinitionRepository definitions,
    IWorkflowTaskRepository tasks,
    AuthorizationGuard guard,
    ILogger<RetireTaskDefinitionCommandHandler> logger)
    : IRequestHandler<RetireTaskDefinitionCommand, TaskDefinitionDto>
{
    public async Task<TaskDefinitionDto> Handle(RetireTaskDefinitionCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, WorkflowPermissions.DefinitionManage,
            new AuthorizationContext(ResourceType: "workflow.task"), ct);

        var definition = await definitions.GetByCodeAsync(cmd.Code, ct)
            ?? throw new TaskDefinitionNotFoundException(cmd.Code);

        if (await tasks.ListOpenByDefinitionAsync(cmd.Code, ct) is { Count: > 0 })
            throw new TaskDefinitionInUseException(cmd.Code);

        definition.Retire(cmd.ActorId, DateTime.UtcNow);
        await definitions.UpdateAsync(definition, ct);
        logger.TaskDefinitionRetired(definition.Code);
        return definition.ToDto();
    }
}

internal static class WorkflowCommandHelpers
{
    /// <summary>
    /// Loads the task for a guarded mutation. Ordering guarantees that
    /// authorization is evaluated before any persistence side effect.
    /// </summary>
    public static async Task<WorkflowTask> LoadForMutationAsync(
        IWorkflowTaskRepository tasks,
        Guid id,
        CancellationToken ct) =>
        await tasks.GetByIdAsync(id, ct) ?? throw new WorkflowTaskNotFoundException(id);

    /// <summary>
    /// Saves the aggregate. The domain events are published BEFORE SaveChanges
    /// so the forwarded integration events are written into the outbox and
    /// committed atomically with the task change (ADR-015, ratified — the
    /// Workflow outbox gate).
    /// </summary>
    public static async Task SaveAsync(
        WorkflowTask task,
        IWorkflowTaskRepository tasks,
        IMediator mediator,
        CancellationToken ct)
    {
        await WorkflowEventsPublisher.PublishAsync(task, mediator, ct);
        await tasks.UpdateAsync(task, ct);
    }

    internal static async Task EnsureUnitExistsAsync(
        IOrganizationUnitReferenceRepository units, Guid? unitId, CancellationToken ct)
    {
        if (unitId is null)
            return;

        if (!await units.ExistsAsync(unitId.Value, ct))
            throw new InvalidTaskScopeException(unitId.Value);
    }
}