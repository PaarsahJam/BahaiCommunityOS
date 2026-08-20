using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Workflow.Domain.Aggregates;

/// <summary>
/// A single assignee reference owned by a <see cref="TaskAssignment"/> (ADR-024).
/// Only the stable person id is stored — names are never persisted; they are
/// resolved through the Community API at read time. Assignee ids appear in
/// integration events (identifier-only), never notes or other PII.
/// </summary>
public sealed class TaskAssignee : Entity<Guid>
{
    private TaskAssignee() : base(Guid.Empty)
    {
    }

    internal TaskAssignee(Guid id, Guid assigneeId) : base(id)
    {
        AssigneeId = assigneeId;
    }

    public Guid AssigneeId { get; private set; }
}