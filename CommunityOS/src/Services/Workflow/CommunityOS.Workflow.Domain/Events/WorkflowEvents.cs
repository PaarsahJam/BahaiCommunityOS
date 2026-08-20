using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Workflow.Domain.Events;

/// <summary>Raised when a workflow task is created (ADR-024).</summary>
public sealed record WorkflowTaskCreatedEvent(
    Guid TaskId,
    string DefinitionCode,
    string DomainType,
    Guid DomainEntityId,
    Guid? OrganizationUnitId,
    Guid CreatedBy) : DomainEvent;

/// <summary>Raised when assignee(s) are assigned or reassigned.</summary>
public sealed record WorkflowTaskAssignedEvent(
    Guid TaskId,
    string DefinitionCode,
    IReadOnlyList<Guid> AssigneeIds,
    Guid AssignedBy) : DomainEvent;

/// <summary>Raised on <c>Assigned → In Progress</c>.</summary>
public sealed record WorkflowTaskStartedEvent(
    Guid TaskId,
    string DefinitionCode,
    Guid StartedBy) : DomainEvent;

/// <summary>Raised on <c>In Progress → Completed</c> with an outcome.</summary>
public sealed record WorkflowTaskCompletedEvent(
    Guid TaskId,
    string DefinitionCode,
    string Outcome,
    Guid CompletedBy) : DomainEvent;

/// <summary>Raised on any non-terminal → <c>Cancelled</c>.</summary>
public sealed record WorkflowTaskCancelledEvent(
    Guid TaskId,
    string DefinitionCode,
    Guid CancelledBy) : DomainEvent;

/// <summary>Raised on an explicit escalation to a different assignee.</summary>
public sealed record WorkflowTaskEscalatedEvent(
    Guid TaskId,
    string DefinitionCode,
    IReadOnlyList<Guid> EscalatedTo,
    Guid EscalatedBy) : DomainEvent;