using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Workflow.Domain.Aggregates;

/// <summary>
/// An additional organization-unit scope for a task (ADR-024). The primary scope
/// lives on <see cref="WorkflowTask.OrganizationUnitId"/>; these rows add more
/// scopes. Access is granted when the caller holds the permission at any of the
/// task's scopes. Unit ids are references to the Organization read-model
/// projection — never FKs into the Organization database (ADR-016).
/// </summary>
public sealed class TaskScope : Entity<Guid>
{
    private TaskScope() : base(Guid.Empty)
    {
    }

    internal TaskScope(Guid id, Guid organizationUnitId) : base(id)
    {
        OrganizationUnitId = organizationUnitId;
    }

    public Guid OrganizationUnitId { get; private set; }
}