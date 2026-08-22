using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Search.Application.Permissions;
using CommunityOS.Search.Domain;

namespace CommunityOS.Search.Application.Authorization;

/// <summary>
/// Builds the resource-level authorization contexts for a projected search row
/// (ADR-026). A row may belong to multiple organization scopes (primary plus
/// additional); access is granted when the caller holds the permission at ANY
/// of the row's scopes. Every check passes resource type "search.result" and
/// the source id so relationship tuples and organization-scoped grants compose
/// exactly as in Records/Workflow/Notifications. Fail-closed: any inability to
/// establish the grant is a Deny.
/// </summary>
internal static class SearchAuthorization
{
    /// <summary>
    /// One context per organization-unit scope (primary plus additional), or a
    /// single global resource context when the row has no unit scope.
    /// </summary>
    public static IReadOnlyList<AuthorizationContext> ContextsFor(SearchProjectionRow row)
    {
        var units = new List<Guid>();
        if (row.OrganizationUnitId is { } unit) units.Add(unit);
        units.AddRange(row.AdditionalScopes);

        if (units.Count == 0)
            return [new AuthorizationContext(ResourceType: SearchPermissions.ResourceType, ResourceId: row.SourceId)];

        return units
            .Distinct()
            .Select(unit => new AuthorizationContext(unit, SearchPermissions.ResourceType, row.SourceId))
            .ToList();
    }
}
