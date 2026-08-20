namespace CommunityOS.Workflow.Domain.Exceptions;

/// <summary>404 — task not found or not readable (indistinguishable).</summary>
public sealed class WorkflowTaskNotFoundException(Guid taskId)
    : Exception($"Workflow task '{taskId}' was not found.");

/// <summary>404 — task definition not found.</summary>
public sealed class TaskDefinitionNotFoundException(string code)
    : Exception($"Task definition '{code}' was not found.");

/// <summary>400 — lifecycle transition is not permitted.</summary>
public sealed class InvalidWorkflowTaskTransitionException(Guid taskId, string from, string to)
    : Exception($"Workflow task '{taskId}' cannot transition from '{from}' to '{to}'.");

/// <summary>403 — the actor is not an assignee of the task.</summary>
public sealed class TaskNotAssignableToActorException(Guid taskId)
    : Exception($"Workflow task '{taskId}' cannot be acted on by a subject that is not an assignee.");

/// <summary>400 — the completion outcome is not permitted by the task definition.</summary>
public sealed class InvalidWorkflowOutcomeException(Guid taskId, string outcome)
    : Exception($"Outcome '{outcome}' is not permitted for workflow task '{taskId}'.");

/// <summary>400 — the request references a definition that does not exist.</summary>
public sealed class InvalidTaskDefinitionReferenceException(string code)
    : Exception($"Task definition '{code}' is not a known definition reference.");

/// <summary>409 — a definition with the same code already exists.</summary>
public sealed class DuplicateTaskDefinitionException(string code)
    : Exception($"Task definition '{code}' already exists.");

/// <summary>409 — a definition that is in use cannot be retired.</summary>
public sealed class TaskDefinitionInUseException(string code)
    : Exception($"Task definition '{code}' has open tasks and cannot be retired.");

/// <summary>409 — a retired definition cannot be updated.</summary>
public sealed class RetiredTaskDefinitionUpdateException(string code)
    : Exception($"Task definition '{code}' is retired and cannot be updated.");

/// <summary>400 — a task references a domain type that is not recognized.</summary>
public sealed class InvalidTaskDomainTypeException(string domainType)
    : Exception($"Task domain type '{domainType}' is not recognized.");

/// <summary>400 — a domain-bound task requires a domain entity id.</summary>
public sealed class TaskDomainEntityIdRequiredException(string definitionCode)
    : Exception($"Task definition '{definitionCode}' is domain-bound and requires a domain entity id.");

/// <summary>400 — an organization scope reference is not a known unit reference.</summary>
public sealed class InvalidTaskScopeException(Guid organizationUnitId)
    : Exception($"Organization unit scope '{organizationUnitId}' is not a known unit reference.");