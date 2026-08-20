namespace CommunityOS.Workflow.Application.DTOs;

public sealed record WorkflowTaskSummaryDto(
    Guid Id,
    string DefinitionCode,
    string DomainType,
    Guid? DomainEntityId,
    string Status,
    Guid? OrganizationUnitId,
    IReadOnlyList<Guid> AssigneeIds,
    DateTime? DueOn,
    bool Overdue,
    string? Outcome,
    DateTime UpdatedOn);

public sealed record WorkflowTaskDto(
    Guid Id,
    string DefinitionCode,
    string DomainType,
    Guid? DomainEntityId,
    string Status,
    Guid? OrganizationUnitId,
    IReadOnlyList<Guid> AdditionalScopes,
    IReadOnlyList<Guid> AssigneeIds,
    DateTime? DueOn,
    bool Overdue,
    Guid? EscalatedTo,
    Guid? EscalatedBy,
    DateTime? EscalatedOn,
    string? Outcome,
    Guid OriginatorId,
    Guid CreatedBy,
    DateTime CreatedOn,
    Guid UpdatedBy,
    DateTime UpdatedOn,
    Guid? StartedBy,
    DateTime? StartedOn,
    Guid? CompletedBy,
    DateTime? CompletedOn);

/// <summary>
/// Sensitive task fields/notes, returned only under
/// <c>workflow.task.read.sensitive</c>. Notes and escalation reasons are never
/// exported in integration events or logs.
/// </summary>
public sealed record WorkflowTaskSensitiveFieldsDto(
    Guid TaskId,
    string? Notes,
    string? EscalationReason);

public sealed record TaskDefinitionDto(
    string Code,
    string DisplayName,
    string Description,
    string DomainType,
    IReadOnlyList<string> PermittedOutcomes,
    string? DueIn,
    bool RequiresHumanReview,
    bool IsActive,
    Guid CreatedBy,
    DateTime CreatedOn,
    Guid UpdatedBy,
    DateTime UpdatedOn);

public sealed record TaskActivityDto(
    Guid Id,
    Guid TaskId,
    string Action,
    Guid ActorId,
    DateTime OccurredOn,
    string? Outcome);