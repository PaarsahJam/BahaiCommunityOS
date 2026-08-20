using CommunityOS.Workflow.Domain.Aggregates;

namespace CommunityOS.Workflow.Domain.Repositories;

/// <summary>
/// Persistence abstraction for the WorkflowTask aggregate (ADR-024).
/// Implementations live in the Infrastructure layer (EF Core). Methods return
/// nullable results and throw nothing — command handlers translate a null into
/// the corresponding domain exception so read-404 and write-404 stay
/// indistinguishable.
/// </summary>
public interface IWorkflowTaskRepository
{
    Task<WorkflowTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<WorkflowTask>> ListAsync(CancellationToken cancellationToken = default);

    Task<List<WorkflowTask>> ListByOrganizationUnitAsync(
        Guid organizationUnitId,
        CancellationToken cancellationToken = default);

    /// <summary>The single open task for a (definition, domain entity) pair, if any.</summary>
    Task<WorkflowTask?> FindOpenAsync(
        string definitionCode,
        string domainType,
        Guid domainEntityId,
        CancellationToken cancellationToken = default);

    Task<List<WorkflowTask>> ListOpenByDefinitionAsync(
        string definitionCode,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsOpenAsync(
        string definitionCode,
        string domainType,
        Guid domainEntityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new task. When a concurrent create already opened a task for
    /// the same (definition, domain entity), the unique filtered index rejects the
    /// insert and the existing open task is returned instead — keeping creation
    /// idempotent under concurrency.
    /// </summary>
    Task<WorkflowTask> AddIfAbsentAsync(WorkflowTask task, CancellationToken cancellationToken = default);

    Task UpdateAsync(WorkflowTask task, CancellationToken cancellationToken = default);
}

/// <summary>Persistence abstraction for the TaskDefinition catalog.</summary>
public interface ITaskDefinitionRepository
{
    Task<TaskDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<List<TaskDefinition>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Whether any open (non-terminal) task references the definition.</summary>
    Task<bool> HasOpenTasksAsync(string code, CancellationToken cancellationToken = default);

    Task AddAsync(TaskDefinition definition, CancellationToken cancellationToken = default);

    Task UpdateAsync(TaskDefinition definition, CancellationToken cancellationToken = default);
}

/// <summary>Persistence abstraction for the Organization unit read-model projection.</summary>
public interface IOrganizationUnitReferenceRepository
{
    Task<bool> ExistsAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);

    Task AddAsync(OrganizationUnitReference reference, CancellationToken cancellationToken = default);

    Task UpdateAsync(OrganizationUnitReference reference, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);
}