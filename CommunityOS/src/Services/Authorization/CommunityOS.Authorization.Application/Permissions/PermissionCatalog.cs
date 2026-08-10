namespace CommunityOS.Authorization.Application.Permissions;

/// <summary>
/// Central registry of well-known permission names. Permissions name
/// capabilities (<c>domain.entity.action</c>), never UI affordances.
/// </summary>
public static class PermissionCatalog
{
    // Authorization administration capabilities (authz.*). These govern the
    // authorization service itself; a global grant of an authz.* permission is
    // authority to administer at every scope. Data permissions never cascade
    // beyond their scope (fail closed for resource access).
    public const string AuthzCheck = "authz.check";
    public const string AuthzRoleList = "authz.role.list";
    public const string AuthzRoleCreate = "authz.role.create";
    public const string AuthzRoleUpdate = "authz.role.update";
    public const string AuthzRoleAssign = "authz.role.assign";
    public const string AuthzRoleRevoke = "authz.role.revoke";
    public const string AuthzRelationshipWrite = "authz.relationship.write";
    public const string AuthzRelationshipRead = "authz.relationship.read";
    public const string AuthzDelegationGrant = "authz.delegation.grant";
    public const string AuthzDelegationRevoke = "authz.delegation.revoke";
    public const string AuthzBreakGlassApprove = "authz.breakglass.approve";
    public const string AuthzBreakGlassRevoke = "authz.breakglass.revoke";
    public const string AuthzBreakGlassList = "authz.breakglass.list";

    /// <summary>
    /// Permissions that govern authorization administration. Grants of these
    /// permissions participate in the organization scope hierarchy; a global
    /// grant authorizes administration at every scope.
    /// </summary>
    public static bool IsAdministrationPermission(string permission) =>
        permission.StartsWith("authz.", StringComparison.Ordinal);

    /// <summary>
    /// Example permissions for future domains, documented for consistency.
    /// These are capabilities, not UI buttons, and are NOT enforced here.
    /// </summary>
    public static readonly string[] DocumentedExamples =
    [
        "community.person.read",
        "community.person.update",
        "records.record.read",
        "records.record.create",
        "records.record.verify",
        "documents.document.read",
        "documents.document.upload",
        "documents.document.delete",
        "correspondence.letter.create",
        "correspondence.letter.submit",
        "correspondence.letter.read",
        "workflow.task.read",
        "workflow.task.complete"
    ];
}
