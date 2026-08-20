namespace CommunityOS.Contracts.Workflow;

/// <summary>
/// Raised when a workflow task is created from a task definition. Carries
/// stable identifiers only — the task id, definition code, domain reference and
/// primary organization scope. Never task notes, assignee details or PII
/// (ADR-024).
/// </summary>
public sealed record WorkflowTaskCreated(
    Guid TaskId,
    string DefinitionCode,
    string DomainType,
    Guid DomainEntityId,
    Guid? OrganizationUnitId,
    Guid CreatedBy,
    DateTime OccurredOn);

/// <summary>
/// Raised when a task is assigned or reassigned to one or more actors. Carries
/// only stable actor identifiers.
/// </summary>
public sealed record WorkflowTaskAssigned(
    Guid TaskId,
    string DefinitionCode,
    IReadOnlyList<Guid> AssigneeIds,
    Guid AssignedBy,
    DateTime OccurredOn);

/// <summary>
/// Raised when a task transitions <c>Assigned → In Progress</c>.
/// </summary>
public sealed record WorkflowTaskStarted(
    Guid TaskId,
    string DefinitionCode,
    Guid StartedBy,
    DateTime OccurredOn);

/// <summary>
/// Raised when a task transitions <c>In Progress → Completed</c> with a
/// definition-permitted outcome. Carries only the outcome code.
/// </summary>
public sealed record WorkflowTaskCompleted(
    Guid TaskId,
    string DefinitionCode,
    string Outcome,
    Guid CompletedBy,
    DateTime OccurredOn);

/// <summary>
/// Raised when a task transitions to <c>Cancelled</c>.
/// </summary>
public sealed record WorkflowTaskCancelled(
    Guid TaskId,
    string DefinitionCode,
    Guid CancelledBy,
    DateTime OccurredOn);

/// <summary>
/// Raised when a task is escalated to one or more actors. Carries only stable
/// actor identifiers.
/// </summary>
public sealed record WorkflowTaskEscalated(
    Guid TaskId,
    string DefinitionCode,
    IReadOnlyList<Guid> EscalatedTo,
    Guid EscalatedBy,
    DateTime OccurredOn);