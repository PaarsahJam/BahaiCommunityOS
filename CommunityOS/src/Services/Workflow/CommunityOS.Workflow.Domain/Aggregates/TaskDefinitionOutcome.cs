using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Workflow.Domain.Aggregates;

/// <summary>
/// A permitted outcome code on a task definition (ADR-024). Outcomes are stable
/// strings (e.g. <c>verified</c>, <c>rejected</c>, <c>approved</c>,
/// <c>clarification-requested</c>); completing a task with a non-permitted
/// outcome is rejected by the domain.
/// </summary>
public sealed class TaskDefinitionOutcome : Entity<Guid>
{
    private TaskDefinitionOutcome() : base(Guid.Empty)
    {
        Code = null!;
    }

    internal TaskDefinitionOutcome(Guid id, string code) : base(id)
    {
        Code = code;
    }

    public string Code { get; private set; }
}