using CommunityOS.Audit.Application.Permissions;
using CommunityOS.Audit.Domain;
using CommunityOS.Authorization.Application.Authorization;

namespace CommunityOS.Audit.Application.Authorization;

/// <summary>
/// Builds the resource-level authorization context for a journal entry
/// (ADR-027 decision 11). An entry carries at most one organization scope;
/// access is granted when the caller holds the permission at that scope —
/// national/regional/local grants cover descendants through the Authorization
/// service's hierarchy resolution. Entries without an organization context are
/// visible only to global-scope holders: they resolve to a single global
/// resource context, which only a Global grant matches. Fail-closed: any
/// inability to establish the grant is a Deny.
/// </summary>
internal static class AuditAuthorization
{
    public static IReadOnlyList<AuthorizationContext> ContextsFor(AuditEntryRow row) =>
        row.OrganizationUnitId is { } unit
            ? [new AuthorizationContext(unit, AuditPermissions.ResourceType, row.Id)]
            : [new AuthorizationContext(ResourceType: AuditPermissions.ResourceType, ResourceId: row.Id)];
}
