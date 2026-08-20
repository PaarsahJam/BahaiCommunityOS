using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;
using CommunityOS.Workflow.Domain.Enumerations;
using CommunityOS.Workflow.Domain.Events;
using CommunityOS.Workflow.Domain.Exceptions;

namespace CommunityOS.Workflow.Domain.Aggregates;

/// <summary>
/// Aggregate root of the Workflow bounded context (ADR-024): a task/work-item
/// instance routed, assigned, tracked and escalated for human review and
/// approval work. Carries the definition code, a stable domain reference
/// (<c>DomainType</c> + <c>DomainEntityId</c>, or free-standing <c>general</c>
/// work), lifecycle status, an originator, assignees, organization scopes,
/// optional <c>DueOn</c>, escalation state, outcome, an append-only activity
/// history, and sensitive task notes. Task state is never domain state; the
/// owning service performs the underlying governance action.
/// </summary>
public sealed class WorkflowTask : AggregateRoot<Guid>
{
    private readonly List<TaskAssignment> _assignments = [];
    private readonly List<TaskActivity> _activity = [];
    private readonly List<TaskScope> _scopes = [];

    private WorkflowTask() : base(Guid.Empty)
    {
        DefinitionCode = null!;
        DomainType = null!;
        OriginatorId = Guid.Empty;
        Status = WorkflowStatus.Created;
    }

    private WorkflowTask(
        Guid id,
        string definitionCode,
        string domainType,
        Guid? domainEntityId,
        Guid? organizationUnitId,
        IReadOnlyList<Guid> additionalScopes,
        IReadOnlyList<Guid> assigneeIds,
        DateTime? dueOn,
        string? notes,
        Guid originatorId,
        Guid createdBy,
        DateTime occurredOn) : base(id)
    {
        DefinitionCode = definitionCode;
        DomainType = domainType;
        DomainEntityId = domainEntityId;
        OrganizationUnitId = organizationUnitId;
        DueOn = dueOn;
        Notes = notes;
        OriginatorId = originatorId;
        CreatedBy = createdBy;
        CreatedOn = occurredOn.ToUniversalTime();
        UpdatedBy = createdBy;
        UpdatedOn = occurredOn.ToUniversalTime();
        Status = WorkflowStatus.Created;
        _scopes.AddRange(
            additionalScopes.Distinct().Select(s => new TaskScope(Guid.NewGuid(), s)));

        if (assigneeIds is { Count: > 0 })
        {
            _assignments.Add(new TaskAssignment(
                Guid.NewGuid(), assigneeIds, createdBy, occurredOn.ToUniversalTime()));
            Status = WorkflowStatus.Assigned;
        }
    }

    public string DefinitionCode { get; private set; }

    public string DomainType { get; private set; }

    /// <summary>Stable domain entity id; null for free-standing <c>general</c> work.</summary>
    public Guid? DomainEntityId { get; private set; }

    /// <summary>Primary organization scope. Nullable for tasks without a unit scope.</summary>
    public Guid? OrganizationUnitId { get; private set; }

    public WorkflowStatus Status { get; private set; }

    /// <summary>Sensitive. Stored; visible only under <c>workflow.task.read.sensitive</c>.</summary>
    public string? Notes { get; private set; }

    public DateTime? DueOn { get; private set; }

    public Guid OriginatorId { get; private set; }

    public Guid? EscalatedTo { get; private set; }

    public Guid? EscalatedBy { get; private set; }

    public DateTime? EscalatedOn { get; private set; }

    public string? EscalationReason { get; private set; }

    public string? Outcome { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public Guid UpdatedBy { get; private set; }

    public DateTime UpdatedOn { get; private set; }

    public Guid? StartedBy { get; private set; }

    public DateTime? StartedOn { get; private set; }

    public Guid? CompletedBy { get; private set; }

    public DateTime? CompletedOn { get; private set; }

    public IReadOnlyList<TaskAssignment> Assignments => _assignments.AsReadOnly();

    public IReadOnlyList<TaskActivity> Activity => _activity.AsReadOnly();

    public IReadOnlyList<TaskScope> Scopes => _scopes.AsReadOnly();

    /// <summary>Every organization-unit scope: primary plus additional scopes.</summary>
    public IEnumerable<Guid> AllOrganizationUnitIds =>
        new[] { OrganizationUnitId }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Concat(_scopes.Select(s => s.OrganizationUnitId))
            .Distinct();

    public IReadOnlyList<Guid> AssigneeIds =>
        _assignments.Count == 0
            ? []
            : _assignments[^1].AssigneeIds;

    public bool IsTerminal =>
        Status == WorkflowStatus.Completed || Status == WorkflowStatus.Cancelled;

    public bool IsOverdue(DateTime asOf) =>
        !IsTerminal && DueOn.HasValue && asOf > DueOn.Value;

    public bool IsAssignee(Guid actorId) =>
        AssigneeIds.Contains(actorId);

    /// <summary>
    /// Creates a task in <c>Created</c> (or <c>Assigned</c> when <c>assigneeIds</c>
    /// is provided). Creation is idempotent per (definition, domain entity) at
    /// the repository/handler boundary; the aggregate itself always creates a new
    /// task.
    /// </summary>
    public static WorkflowTask Create(
        string definitionCode,
        string domainType,
        Guid? domainEntityId,
        Guid? organizationUnitId,
        IReadOnlyList<Guid> additionalScopes,
        IReadOnlyList<Guid> assigneeIds,
        DateTime? dueOn,
        string? notes,
        Guid originatorId,
        Guid createdBy,
        DateTime occurredOn)
    {
        Guard.NotNullOrWhiteSpace(definitionCode, nameof(definitionCode));
        Guard.MaxLength(definitionCode, 100, nameof(definitionCode));
        Guard.NotNullOrWhiteSpace(domainType, nameof(domainType));
        Guard.MaxLength(domainType, 50, nameof(domainType));

        if (!WorkflowDomainTypes.IsValid(domainType))
            throw new InvalidTaskDomainTypeException(domainType);

        if (!string.Equals(domainType, WorkflowDomainTypes.General, StringComparison.OrdinalIgnoreCase) &&
            !domainEntityId.HasValue)
            throw new TaskDomainEntityIdRequiredException(domainType);

        Guard.NotDefault(originatorId, nameof(originatorId));
        Guard.NotDefault(createdBy, nameof(createdBy));
        if (notes is not null)
            Guard.MaxLength(notes, 2000, nameof(notes));

        var task = new WorkflowTask(
            Guid.NewGuid(),
            definitionCode.Trim(),
            domainType.Trim().ToLowerInvariant(),
            domainEntityId,
            organizationUnitId,
            additionalScopes ?? [],
            assigneeIds ?? [],
            dueOn,
            notes,
            originatorId,
            createdBy,
            occurredOn);

        task._activity.Add(new TaskActivity(
            Guid.NewGuid(),
            WorkflowActivityActions.Created,
            createdBy,
            occurredOn.ToUniversalTime(),
            outcome: null,
            notes: null));

        if (task.AssigneeIds.Count > 0)
        {
            task._activity.Add(new TaskActivity(
                Guid.NewGuid(),
                WorkflowActivityActions.Assigned,
                createdBy,
                occurredOn.ToUniversalTime(),
                outcome: null,
                notes: null));
        }

        task.RaiseDomainEvent(new WorkflowTaskCreatedEvent(
            task.Id,
            task.DefinitionCode,
            task.DomainType,
            task.DomainEntityId ?? Guid.Empty,
            task.OrganizationUnitId,
            task.CreatedBy));

        return task;
    }

    /// <summary>
    /// Assigns/reassigns assignee(s). Legal from <c>Created</c>, <c>Assigned</c>
    /// and <c>In Progress</c>; transitions the task to <c>Assigned</c>.
    /// </summary>
    public void Assign(IReadOnlyList<Guid> assigneeIds, Guid assignedBy, DateTime occurredOn)
    {
        var assignees = (assigneeIds ?? []).Distinct().ToList();
        if (assignees.Count == 0)
            throw new ArgumentException("At least one assignee is required.", nameof(assigneeIds));

        if (Status != WorkflowStatus.Created &&
            Status != WorkflowStatus.Assigned &&
            Status != WorkflowStatus.InProgress)
            throw new InvalidWorkflowTaskTransitionException(Id, Status.Name, WorkflowStatus.Assigned.Name);

        Guard.NotDefault(assignedBy, nameof(assignedBy));

        _assignments.Add(new TaskAssignment(
            Guid.NewGuid(), assignees, assignedBy, occurredOn.ToUniversalTime()));
        Status = WorkflowStatus.Assigned;
        UpdatedBy = assignedBy;
        UpdatedOn = occurredOn.ToUniversalTime();
        _activity.Add(new TaskActivity(
            Guid.NewGuid(),
            WorkflowActivityActions.Assigned,
            assignedBy,
            occurredOn.ToUniversalTime(),
            outcome: null,
            notes: null));

        RaiseDomainEvent(new WorkflowTaskAssignedEvent(Id, DefinitionCode, assignees, assignedBy));
    }

    /// <summary>
    /// Transitions <c>Assigned → In Progress</c>, or <c>Created → In Progress</c>
    /// when the task was created already self-assigned. Only an assignee can start.
    /// </summary>
    public void Start(Guid actorId, bool adminOverride, DateTime occurredOn)
    {
        if (Status != WorkflowStatus.Assigned &&
            Status != WorkflowStatus.Created)
            throw new InvalidWorkflowTaskTransitionException(Id, Status.Name, WorkflowStatus.InProgress.Name);

        if (!IsAssignee(actorId) && !adminOverride)
            throw new TaskNotAssignableToActorException(Id);

        Guard.NotDefault(actorId, nameof(actorId));

        Status = WorkflowStatus.InProgress;
        StartedBy = actorId;
        StartedOn = occurredOn.ToUniversalTime();
        UpdatedBy = actorId;
        UpdatedOn = occurredOn.ToUniversalTime();
        _activity.Add(new TaskActivity(
            Guid.NewGuid(),
            WorkflowActivityActions.Started,
            actorId,
            occurredOn.ToUniversalTime(),
            outcome: null,
            notes: null));

        RaiseDomainEvent(new WorkflowTaskStartedEvent(Id, DefinitionCode, actorId));
    }

    /// <summary>
    /// Transitions <c>In Progress → Completed</c> with a definition-permitted
    /// outcome. Reject is an outcome, not a state. The completing subject must be
    /// an assignee (or use an administrative override). Completed tasks are
    /// immutable.
    /// </summary>
    public void Complete(
        string outcome,
        string? notes,
        IReadOnlyList<string> permittedOutcomes,
        Guid actorId,
        bool adminOverride,
        DateTime occurredOn)
    {
        if (Status != WorkflowStatus.InProgress)
            throw new InvalidWorkflowTaskTransitionException(Id, Status.Name, WorkflowStatus.Completed.Name);

        if (!IsAssignee(actorId) && !adminOverride)
            throw new TaskNotAssignableToActorException(Id);

        Guard.NotNullOrWhiteSpace(outcome, nameof(outcome));
        Guard.MaxLength(outcome, 50, nameof(outcome));
        if (notes is not null)
            Guard.MaxLength(notes, 2000, nameof(notes));

        if (permittedOutcomes.Count > 0 &&
            !permittedOutcomes.Any(o => string.Equals(o, outcome, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidWorkflowOutcomeException(Id, outcome);

        Guard.NotDefault(actorId, nameof(actorId));

        Outcome = outcome.Trim().ToLowerInvariant();
        if (notes is not null)
            Notes = notes;
        Status = WorkflowStatus.Completed;
        CompletedBy = actorId;
        CompletedOn = occurredOn.ToUniversalTime();
        UpdatedBy = actorId;
        UpdatedOn = occurredOn.ToUniversalTime();
        _activity.Add(new TaskActivity(
            Guid.NewGuid(),
            WorkflowActivityActions.Completed,
            actorId,
            occurredOn.ToUniversalTime(),
            Outcome,
            notes));

        RaiseDomainEvent(new WorkflowTaskCompletedEvent(Id, DefinitionCode, Outcome, actorId));
    }

    /// <summary>
    /// Reconciliation path used by consumers (Records/Knowledge): idempotently
    /// completes an open task with the owning service's authoritative outcome.
    /// Bypasses assignee and permitted-outcome checks — the outcome comes from the
    /// owning context. No-ops on a terminal task (idempotency).
    /// </summary>
    public void CompleteForReconciliation(string outcome, Guid completedBy, DateTime occurredOn)
    {
        if (IsTerminal)
            return;

        Guard.NotNullOrWhiteSpace(outcome, nameof(outcome));
        Guard.MaxLength(outcome, 50, nameof(outcome));

        Outcome = outcome.Trim().ToLowerInvariant();
        Status = WorkflowStatus.Completed;
        CompletedBy = completedBy;
        CompletedOn = occurredOn.ToUniversalTime();
        UpdatedBy = completedBy;
        UpdatedOn = occurredOn.ToUniversalTime();
        _activity.Add(new TaskActivity(
            Guid.NewGuid(),
            WorkflowActivityActions.Completed,
            completedBy,
            occurredOn.ToUniversalTime(),
            Outcome,
            notes: null));

        RaiseDomainEvent(new WorkflowTaskCompletedEvent(Id, DefinitionCode, Outcome, completedBy));
    }

    /// <summary>Transitions any non-terminal state → <c>Cancelled</c>.</summary>
    public void Cancel(Guid actorId, DateTime occurredOn)
    {
        if (IsTerminal)
            throw new InvalidWorkflowTaskTransitionException(Id, Status.Name, WorkflowStatus.Cancelled.Name);

        Guard.NotDefault(actorId, nameof(actorId));

        Status = WorkflowStatus.Cancelled;
        UpdatedBy = actorId;
        UpdatedOn = occurredOn.ToUniversalTime();
        _activity.Add(new TaskActivity(
            Guid.NewGuid(),
            WorkflowActivityActions.Cancelled,
            actorId,
            occurredOn.ToUniversalTime(),
            outcome: null,
            notes: null));

        RaiseDomainEvent(new WorkflowTaskCancelledEvent(Id, DefinitionCode, actorId));
    }

    /// <summary>
    /// Escalates the task to a different assignee with a reason. Legal from
    /// <c>Assigned</c> and <c>In Progress</c>; reassigns to <c>escalateTo</c> and
    /// records the escalation — it does not change the lifecycle status.
    /// </summary>
    public void Escalate(Guid escalateTo, string reason, Guid escalatedBy, DateTime occurredOn)
    {
        if (Status != WorkflowStatus.Assigned && Status != WorkflowStatus.InProgress)
            throw new InvalidWorkflowTaskTransitionException(Id, Status.Name, WorkflowStatus.Assigned.Name);

        if (escalateTo == Guid.Empty)
            throw new ArgumentException("Escalation target cannot be empty.", nameof(escalateTo));

        Guard.NotNullOrWhiteSpace(reason, nameof(reason));
        Guard.MaxLength(reason, 2000, nameof(reason));
        Guard.NotDefault(escalatedBy, nameof(escalatedBy));

        EscalatedTo = escalateTo;
        EscalatedBy = escalatedBy;
        EscalatedOn = occurredOn.ToUniversalTime();
        EscalationReason = reason.Trim();
        _assignments.Add(new TaskAssignment(
            Guid.NewGuid(), [escalateTo], escalatedBy, occurredOn.ToUniversalTime()));
        UpdatedBy = escalatedBy;
        UpdatedOn = occurredOn.ToUniversalTime();
        _activity.Add(new TaskActivity(
            Guid.NewGuid(),
            WorkflowActivityActions.Escalated,
            escalatedBy,
            occurredOn.ToUniversalTime(),
            outcome: null,
            reason.Trim()));

        RaiseDomainEvent(new WorkflowTaskEscalatedEvent(Id, DefinitionCode, [escalateTo], escalatedBy));
    }

    /// <summary>
    /// Appends a sensitive note (e.g. assigned on complete/escalate) to the
    /// activity history. Notes are stored, never exported.
    /// </summary>
    public void AddNote(string notes, Guid actorId, DateTime occurredOn)
    {
        Guard.NotNullOrWhiteSpace(notes, nameof(notes));
        Guard.MaxLength(notes, 2000, nameof(notes));

        Notes = notes;
        _activity.Add(new TaskActivity(
            Guid.NewGuid(),
            WorkflowActivityActions.Note,
            actorId,
            occurredOn.ToUniversalTime(),
            outcome: null,
            notes));
        UpdatedBy = actorId;
        UpdatedOn = occurredOn.ToUniversalTime();
    }
}