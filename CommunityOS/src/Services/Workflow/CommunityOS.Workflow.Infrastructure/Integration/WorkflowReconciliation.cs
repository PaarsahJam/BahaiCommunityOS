using CommunityOS.Workflow.Application.Pipeline;
using CommunityOS.Workflow.Domain.Aggregates;
using CommunityOS.Workflow.Domain.Enumerations;
using CommunityOS.Workflow.Domain.Repositories;
using MediatR;

namespace CommunityOS.Workflow.Infrastructure.Integration;

/// <summary>
/// Shared reconciliation helpers for the Workflow consumers (ADR-024).
/// Reconcile consumers are create-if-absent / close-if-open and idempotent:
/// duplicate integration events never produce duplicate open tasks and never
/// double-complete an already-completed task. Reconcile-created tasks carry no
/// domain data — only the stable definition/domain references and the
/// originator (the reconcile actor, never PII).
/// </summary>
public static class WorkflowReconciliation
{
    /// <summary>
    /// Well-known in-process actor recorded as the originator/actor for
    /// reconcile-created and reconcile-completed tasks (never a real person id;
    /// not PII).
    /// </summary>
    public static readonly Guid SystemActorId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public const string RecordReviewDefinitionCode = WorkflowDefinitionCodes.RecordReview;
    public const string KnowledgeModerationDefinitionCode = WorkflowDefinitionCodes.KnowledgeModeration;
    public const string KnowledgeAiReviewDefinitionCode = WorkflowDefinitionCodes.KnowledgeAiReview;
    public const string RecordDomainType = WorkflowDomainTypes.Record;
    public const string KnowledgeQuestionDomainType = WorkflowDomainTypes.KnowledgeQuestion;
    public const string KnowledgeAiSuggestionDomainType = WorkflowDomainTypes.KnowledgeAiSuggestion;

    /// <summary>
    /// Creates an open domain-bound task if none exists for
    /// (definition, domainType, domainEntityId). Returns the created task, or
    /// null when an open task already existed. The unique filtered index plus
    /// <see cref="IWorkflowTaskRepository.AddIfAbsentAsync"/> keeps this safe
    /// under concurrent duplicate events.
    /// </summary>
    public static async Task<WorkflowTask?> CreateIfAbsentAsync(
        IWorkflowTaskRepository tasks,
        string definitionCode,
        string domainType,
        Guid domainEntityId,
        Guid originatorId,
        DateTime occurredOn,
        CancellationToken ct)
    {
        if (await tasks.FindOpenAsync(definitionCode, domainType, domainEntityId, ct) is not null)
            return null;

        var task = WorkflowTask.Create(
            definitionCode,
            domainType,
            domainEntityId,
            organizationUnitId: null,
            additionalScopes: [],
            assigneeIds: [],
            dueOn: null,
            notes: null,
            originatorId,
            SystemActorId,
            occurredOn);

        // Reconcile-created tasks are published through the same outbox gate;
        // the create event and the task row commit atomically.
        var persisted = await tasks.AddIfAbsentAsync(task, ct);
        return persisted is not null && persisted.Id == task.Id ? task : null;
    }

    /// <summary>
    /// Completes the open task for (definition, domainType, domainEntityId) with
    /// the owning service's authoritative outcome via
    /// <see cref="WorkflowTask.CompleteForReconciliation"/>. No-ops when no open
    /// task exists. Domain events are published BEFORE the update so the
    /// forwarded completion event and the task change commit atomically
    /// (ADR-015). Returns the completed task, or null when there was nothing to
    /// complete.
    /// </summary>
    public static async Task<WorkflowTask?> CloseIfOpenAsync(
        IWorkflowTaskRepository tasks,
        IMediator mediator,
        string definitionCode,
        string domainType,
        Guid domainEntityId,
        string outcome,
        Guid completedBy,
        DateTime occurredOn,
        CancellationToken ct)
    {
        var task = await tasks.FindOpenAsync(definitionCode, domainType, domainEntityId, ct);
        if (task is null)
            return null;

        task.CompleteForReconciliation(outcome, completedBy, occurredOn);
        await DomainEventPublisher.PublishAsync(task, mediator, ct);
        await tasks.UpdateAsync(task, ct);
        return task;
    }
}