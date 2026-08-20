using CommunityOS.Workflow.Domain.Aggregates;
using CommunityOS.Workflow.Domain.Enumerations;
using CommunityOS.Workflow.Domain.Repositories;
using CommunityOS.Workflow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Workflow.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IWorkflowTaskRepository"/>. Owned
/// collections are included explicitly so the aggregate graph is fully
/// materialized for guard checks and mutations.
/// </summary>
public sealed class WorkflowTaskRepository(WorkflowDbContext db) : IWorkflowTaskRepository
{
    public Task<WorkflowTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<List<WorkflowTask>> ListAsync(CancellationToken cancellationToken = default) =>
        Query().OrderByDescending(t => t.UpdatedOn).ToListAsync(cancellationToken);

    public Task<List<WorkflowTask>> ListByOrganizationUnitAsync(
        Guid organizationUnitId, CancellationToken cancellationToken = default) =>
        Query()
            .Where(t => t.OrganizationUnitId == organizationUnitId || t.Scopes.Any(s => s.OrganizationUnitId == organizationUnitId))
            .OrderByDescending(t => t.UpdatedOn)
            .ToListAsync(cancellationToken);

    public Task<WorkflowTask?> FindOpenAsync(
        string definitionCode, string domainType, Guid domainEntityId, CancellationToken cancellationToken = default) =>
        Query()
            .FirstOrDefaultAsync(t =>
                t.DefinitionCode == definitionCode &&
                t.DomainType == domainType &&
                t.DomainEntityId == domainEntityId &&
                !t.IsTerminal, cancellationToken);

    public Task<List<WorkflowTask>> ListOpenByDefinitionAsync(
        string definitionCode, CancellationToken cancellationToken = default) =>
        Query()
            .Where(t => t.DefinitionCode == definitionCode && !t.IsTerminal)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsOpenAsync(
        string definitionCode, string domainType, Guid domainEntityId, CancellationToken cancellationToken = default) =>
        db.WorkflowTasks.AnyAsync(t =>
            t.DefinitionCode == definitionCode &&
            t.DomainType == domainType &&
            t.DomainEntityId == domainEntityId &&
            !t.IsTerminal, cancellationToken);

    public async Task<WorkflowTask> AddIfAbsentAsync(WorkflowTask task, CancellationToken cancellationToken = default)
    {
        db.WorkflowTasks.Add(task);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return task;
        }
        catch (DbUpdateException)
        {
            // A concurrent create already opened a task for the same
            // (definition, domain entity); the unique filtered index rejected
            // our insert. Return the existing open task so creation stays
            // idempotent under concurrency.
            if (task.DomainEntityId is { } domainEntityId)
            {
                db.Entry(task).State = EntityState.Detached;
                var existing = await FindOpenAsync(
                    task.DefinitionCode, task.DomainType, domainEntityId, cancellationToken);
                if (existing is not null)
                    return existing;
            }

            throw;
        }
    }

    public async Task UpdateAsync(WorkflowTask task, CancellationToken cancellationToken = default)
    {
        db.WorkflowTasks.Update(task);
        await db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<WorkflowTask> Query() =>
        db.WorkflowTasks
            .Include(t => t.Assignments)
            .Include(t => t.Scopes)
            .Include(t => t.Activity);
}

public sealed class TaskDefinitionRepository(WorkflowDbContext db) : ITaskDefinitionRepository
{
    public Task<TaskDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        db.TaskDefinitions
            .Include(d => d.PermittedOutcomes)
            .FirstOrDefaultAsync(d => d.Code == code, cancellationToken);

    public Task<List<TaskDefinition>> ListAsync(CancellationToken cancellationToken = default) =>
        db.TaskDefinitions
            .Include(d => d.PermittedOutcomes)
            .OrderBy(d => d.Code)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        db.TaskDefinitions.AnyAsync(d => d.Code == code, cancellationToken);

    public Task<bool> HasOpenTasksAsync(string code, CancellationToken cancellationToken = default) =>
        db.WorkflowTasks.AnyAsync(t => t.DefinitionCode == code && !t.IsTerminal, cancellationToken);

    public async Task AddAsync(TaskDefinition definition, CancellationToken cancellationToken = default)
    {
        db.TaskDefinitions.Add(definition);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TaskDefinition definition, CancellationToken cancellationToken = default)
    {
        db.TaskDefinitions.Update(definition);
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class WorkflowOrganizationUnitReferenceRepository(WorkflowDbContext db)
    : IOrganizationUnitReferenceRepository
{
    public Task<bool> ExistsAsync(Guid organizationUnitId, CancellationToken cancellationToken = default) =>
        db.OrganizationUnitReferences.AnyAsync(r => r.OrganizationUnitId == organizationUnitId, cancellationToken);

    public async Task AddAsync(OrganizationUnitReference reference, CancellationToken cancellationToken = default)
    {
        db.OrganizationUnitReferences.Add(reference);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(OrganizationUnitReference reference, CancellationToken cancellationToken = default)
    {
        db.OrganizationUnitReferences.Update(reference);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var reference = await db.OrganizationUnitReferences
            .FirstOrDefaultAsync(r => r.OrganizationUnitId == organizationUnitId, cancellationToken);
        if (reference is not null)
        {
            db.OrganizationUnitReferences.Remove(reference);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}