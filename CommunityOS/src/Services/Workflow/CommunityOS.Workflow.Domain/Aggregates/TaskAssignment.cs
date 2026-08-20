using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Workflow.Domain.Aggregates;

/// <summary>
/// An assignment record on a workflow task (ADR-024): the set of assignee ids,
/// the assigning actor and the effective time. Reassignment and escalation are
/// explicit, audited transitions that append a new assignment record; the
/// current assignees are the most recent record's assignee ids.
/// </summary>
public sealed class TaskAssignment : Entity<Guid>
{
    private readonly List<TaskAssignee> _assignees = [];

    private TaskAssignment() : base(Guid.Empty)
    {
    }

    internal TaskAssignment(
        Guid id,
        IReadOnlyList<Guid> assigneeIds,
        Guid assignedBy,
        DateTime assignedOn) : base(id)
    {
        _assignees.AddRange(
            assigneeIds.Distinct().Select(a => new TaskAssignee(Guid.NewGuid(), a)));
        AssignedBy = assignedBy;
        AssignedOn = assignedOn.ToUniversalTime();
    }

    public IReadOnlyList<TaskAssignee> Assignees => _assignees.AsReadOnly();

    public IReadOnlyList<Guid> AssigneeIds => _assignees.Select(a => a.AssigneeId).ToArray();

    public Guid AssignedBy { get; private set; }

    public DateTime AssignedOn { get; private set; }
}