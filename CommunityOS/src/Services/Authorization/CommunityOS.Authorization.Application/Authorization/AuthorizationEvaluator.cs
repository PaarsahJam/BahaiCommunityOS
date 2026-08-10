using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Application.Logging;
using CommunityOS.Authorization.Application.Permissions;
using CommunityOS.Authorization.Domain.Enumerations;
using CommunityOS.Authorization.Domain.Permissions;
using CommunityOS.Authorization.Domain.Repositories;
using CommunityOS.Authorization.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Authorization.Application.Authorization;

/// <summary>
/// Default in-process authorization evaluator. It is fail-closed: any subject,
/// permission, scope or data state that cannot be safely established results
/// in a Deny. The evaluator combines RBAC (role assignments), delegations,
/// break-glass grants and relationship tuples. It never trusts client-supplied
/// roles, permissions or decisions.
/// </summary>
public sealed class AuthorizationEvaluator(
    IRoleAssignmentRepository roleAssignments,
    IRoleRepository roles,
    IDelegationRepository delegations,
    IBreakGlassRequestRepository breakGlassRequests,
    IAuthorizationRelationshipRepository relationships,
    IOrganizationContextProvider organizationContext,
    ILogger<AuthorizationEvaluator> logger) : IAuthorizationEvaluator
{
    public async Task<AuthorizationDecision> EvaluateAsync(
        AuthorizationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var decisionId = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;

        if (request.SubjectId == Guid.Empty)
            return AuthorizationDecision.Deny(decisionId, AuthorizationDecisionReason.MissingSubject, now);

        if (!PermissionName.IsValid(request.Permission))
            return AuthorizationDecision.Deny(decisionId, AuthorizationDecisionReason.InvalidPermission, now);

        var grants = new List<string>();
        var hadActiveGrant = false;
        var hadPermissionOutOfScope = false;

        // RBAC: effective role assignments for the subject.
        var assignments = await roleAssignments.ListBySubjectAsync(request.SubjectId, ct);
        foreach (var assignment in assignments)
        {
            if (!assignment.IsEffectiveAt(now))
                continue;

            hadActiveGrant = true;

            var role = await roles.GetByIdAsync(assignment.RoleId, ct);
            if (role is null)
            {
                logger.DanglingRole(assignment.RoleId, assignment.Id);
                continue;
            }

            if (!role.Enabled || !role.HasPermission(request.Permission))
                continue;

            if (!await ScopeAppliesAsync(assignment.Scope, request.Permission, request.Context, ct))
            {
                hadPermissionOutOfScope = true;
                continue;
            }

            grants.Add($"role:{role.Code}:{assignment.Id:N}");
        }

        // Delegation grants.
        var delegationItems = await delegations.ListByDelegateAsync(request.SubjectId, ct);
        foreach (var delegation in delegationItems)
        {
            if (!delegation.IsActiveAt(now))
                continue;

            hadActiveGrant = true;

            if (!delegation.GrantsPermission(request.Permission))
                continue;

            if (!await ScopeAppliesAsync(delegation.Scope, request.Permission, request.Context, ct))
            {
                hadPermissionOutOfScope = true;
                continue;
            }

            grants.Add($"delegation:{delegation.Id:N}");
        }

        // Break-glass grants (approved, within window, not revoked).
        var breakGlassItems = await breakGlassRequests.ListByRequesterAsync(request.SubjectId, ct);
        foreach (var bg in breakGlassItems)
        {
            bg.ExpireIfNeeded(now);

            if (!bg.IsActiveAt(now))
                continue;

            hadActiveGrant = true;

            if (!bg.GrantsPermission(request.Permission))
                continue;

            if (!await ScopeAppliesAsync(bg.Scope, request.Permission, request.Context, ct))
            {
                hadPermissionOutOfScope = true;
                continue;
            }

            grants.Add($"breakglass:{bg.Id:N}");
        }

        // Relationship-based grants: resource-exact tuples (e.g. has_permission).
        var relationshipTuples = await relationships.ListBySubjectAsync(request.SubjectId, ct);
        foreach (var relationship in relationshipTuples)
        {
            hadActiveGrant = true;

            if (!relationship.GrantsPermission(request.Permission))
                continue;

            if (request.Context.ResourceId is { } resourceId &&
                request.Context.ResourceType is not null &&
                relationship.ObjectId == resourceId &&
                string.Equals(relationship.ObjectType, request.Context.ResourceType, StringComparison.Ordinal))
            {
                grants.Add($"relationship:{relationship.Id:N}");
            }
            else
            {
                hadPermissionOutOfScope = true;
            }
        }

        if (grants.Count != 0)
            return AuthorizationDecision.Allow(decisionId, grants, now);

        if (!hadActiveGrant)
            return AuthorizationDecision.Deny(decisionId, AuthorizationDecisionReason.DeniedByDefault, now);

        if (hadPermissionOutOfScope)
            return AuthorizationDecision.Deny(decisionId, AuthorizationDecisionReason.ScopeMismatch, now);

        return AuthorizationDecision.Deny(decisionId, AuthorizationDecisionReason.NoPermission, now);
    }

    public async Task<IReadOnlyList<AuthorizationDecision>> EvaluateBatchAsync(
        IReadOnlyList<AuthorizationRequest> requests, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var decisions = new List<AuthorizationDecision>(requests.Count);
        foreach (var request in requests)
            decisions.Add(await EvaluateAsync(request, ct));

        return decisions;
    }

    private async Task<bool> ScopeAppliesAsync(
        AuthorizationScope grantScope,
        string permission,
        AuthorizationContext context,
        CancellationToken ct)
    {
        if (grantScope.IsGlobal)
        {
            // A global grant of an administration permission (authz.*) is
            // authority at every scope. A global grant of a data permission
            // only ever applies to checks without organization/resource
            // context — it never grants access to a specific resource.
            return context.IsGlobal || PermissionCatalog.IsAdministrationPermission(permission);
        }

        if (context.IsGlobal)
            return false;

        // Resource-level check.
        if (context.ResourceType is not null && context.ResourceId is not null)
        {
            if (grantScope.Type == ScopeType.Resource)
                return grantScope.ScopeId == context.ResourceId &&
                       string.Equals(grantScope.ResourceType, context.ResourceType, StringComparison.Ordinal);

            if (!grantScope.IsOrganizationScoped)
                return false;

            if (context.OrganizationUnitId is { } resourceOrgId)
                return await OrgUnitCoversAsync(grantScope.ScopeId!.Value, resourceOrgId, ct);

            return false;
        }

        // Organization-level check.
        if (context.OrganizationUnitId is { } orgUnitId)
        {
            if (!grantScope.IsOrganizationScoped)
                return false;

            return await OrgUnitCoversAsync(grantScope.ScopeId!.Value, orgUnitId, ct);
        }

        return false;
    }

    private Task<bool> OrgUnitCoversAsync(Guid candidateAncestorId, Guid orgUnitId, CancellationToken ct) =>
        candidateAncestorId == orgUnitId
            ? Task.FromResult(true)
            : organizationContext.IsAncestorOrSelfAsync(candidateAncestorId, orgUnitId, ct);
}
