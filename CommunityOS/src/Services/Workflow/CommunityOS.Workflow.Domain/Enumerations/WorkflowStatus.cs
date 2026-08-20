using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Workflow.Domain.Enumerations;

/// <summary>
/// Lifecycle state of a workflow task (ADR-024):
/// <c>Created → Assigned → In Progress → Completed | Cancelled</c>.
/// <c>Completed</c> and <c>Cancelled</c> are terminal; completed tasks are
/// immutable (no reopen, no correction — a domain entity re-entering review
/// gets a new task). <c>Reject</c> is an outcome, never a state.
/// </summary>
public sealed class WorkflowStatus : Enumeration<int>
{
    public static readonly WorkflowStatus Created = new(1, "created");
    public static readonly WorkflowStatus Assigned = new(2, "assigned");
    public static readonly WorkflowStatus InProgress = new(3, "in_progress");
    public static readonly WorkflowStatus Completed = new(4, "completed");
    public static readonly WorkflowStatus Cancelled = new(5, "cancelled");

    private WorkflowStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<WorkflowStatus> All =>
        [Created, Assigned, InProgress, Completed, Cancelled];

    public static WorkflowStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown WorkflowStatus id: {id}");

    public static WorkflowStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown WorkflowStatus name: {name}");
}