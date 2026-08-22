using CommunityOS.Authorization.Application.Permissions;

namespace CommunityOS.Search.Application.Permissions;

/// <summary>
/// Search capabilities (search.*, ratified ADR-026). Sensitive results are a
/// separate capability from ordinary reads, and index administration is
/// restricted to operators. Names are registered in the central catalog.
/// </summary>
public static class SearchPermissions
{
    /// <summary>Resource type used in authorization contexts for projected rows.</summary>
    public const string ResourceType = "search.result";

    public const string ResultRead = PermissionCatalog.SearchResultRead;
    public const string ResultReadSensitive = PermissionCatalog.SearchResultReadSensitive;
    public const string IndexManage = PermissionCatalog.SearchIndexManage;
}
