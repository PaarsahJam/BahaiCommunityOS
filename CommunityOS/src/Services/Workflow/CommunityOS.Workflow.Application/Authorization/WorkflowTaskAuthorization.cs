using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Workflow.Domain.Aggregates;

namespace CommunityOS.Workflow.Application.Authorization;

/// <summary>
/// Builds the resource-level authorization contexts for a workflow task
/// (ADR-024). A task may belong to multiple organization scopes; access is
/// granted when the caller holds the permission at <b>any</b> of the task's
/// scopes (primary or additional). Every check passes
/// <c>resourceType = "workflow.task"</c> and the task id so per-task grants
/// (relationship tuples) and organization-scoped grants compose exactly as in
/// other services. Fail-closed: any inability to establish the grant is a Deny.
/// </summary>
internal static class WorkflowTaskAuthorization
{
    /// <summary>
    /// One context per organization-unit scope (primary plus additional scopes),
    /// or a single global resource context when the task has no unit scope.
    /// </summary>
    public static IReadOnlyList<AuthorizationContext> ContextsFor(WorkflowTask task)
    {
        var units = task.AllOrganizationUnitIds.ToList();
        if (units.Count == 0)
            return [new AuthorizationContext(ResourceType: "workflow.task", ResourceId: task.Id)];

        return units
            .Select(unit => new AuthorizationContext(unit, "workflow.task", task.Id))
            .ToList();
    }

    /// <summary>
    /// True when the caller holds <paramref name="permission"/> at any of the
    /// task's organization scopes (or at the global resource scope when the task
    /// has no unit scope).
    /// </summary>
    public static async Task<bool> HasForTaskAsync(
        AuthorizationGuard guard,
        Guid actorId,
        string permission,
        WorkflowTask task,
        CancellationToken ct)
    {
        foreach (var context in ContextsFor(task))
        {
            if (await guard.HasAsync(actorId, permission, context, ct))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Throws <see cref="AuthorizationForbiddenException"/> when the caller does
    /// not hold <paramref name="permission"/> at any of the task's scopes.
    /// </summary>
    public static async Task RequireForTaskAsync(
        AuthorizationGuard guard,
        Guid actorId,
        string permission,
        WorkflowTask task,
        CancellationToken ct)
    {
        if (!await HasForTaskAsync(guard, actorId, permission, task, ct))
            throw new AuthorizationForbiddenException(permission);
    }
}