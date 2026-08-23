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

    // Records capabilities (records.*, ratified ADR-023). Record lifecycle,
    // metadata and sensitive-field access are separate capabilities; a
    // sensitive-field read requires records.record.read.sensitive in addition
    // to records.record.read. records.record.admin is the administrative
    // override used when an active hold blocks deactivation.
    public const string RecordCreate = "records.record.create";
    public const string RecordRead = "records.record.read";
    public const string RecordReadSensitive = "records.record.read.sensitive";
    public const string RecordUpdate = "records.record.update";
    public const string RecordSubmit = "records.record.submit";
    public const string RecordVerify = "records.record.verify";
    public const string RecordCorrect = "records.record.correct";
    public const string RecordArchive = "records.record.archive";
    public const string RecordDeactivate = "records.record.deactivate";
    public const string RecordRestore = "records.record.restore";
    public const string RecordClassify = "records.record.classify";
    public const string RecordScopeManage = "records.record.scope.manage";
    public const string RecordEvidenceManage = "records.record.evidence.manage";
    public const string RetentionManage = "records.retention.manage";
    public const string HoldManage = "records.hold.manage";
    public const string CategoryManage = "records.category.manage";
    public const string RecordAdmin = "records.record.admin";

    // Workflow capabilities (workflow.*, ratified ADR-024). Task lifecycle
    // actions are separate capabilities; sensitive-field read requires
    // workflow.task.read.sensitive in addition to workflow.task.read.
    // workflow.task.admin is the administrative override for start/complete.
    public const string WorkflowTaskRead = "workflow.task.read";
    public const string WorkflowTaskReadSensitive = "workflow.task.read.sensitive";
    public const string WorkflowTaskCreate = "workflow.task.create";
    public const string WorkflowTaskAssign = "workflow.task.assign";
    public const string WorkflowTaskStart = "workflow.task.start";
    public const string WorkflowTaskComplete = "workflow.task.complete";
    public const string WorkflowTaskCancel = "workflow.task.cancel";
    public const string WorkflowTaskEscalate = "workflow.task.escalate";
    public const string WorkflowDefinitionRead = "workflow.definition.read";
    public const string WorkflowDefinitionManage = "workflow.definition.manage";
    public const string WorkflowTaskAdmin = "workflow.task.admin";

    // Notifications capabilities (notifications.*, ratified ADR-025). Inbox and
    // sensitive-field reads are separate capabilities; reading sensitive fields
    // requires notifications.notification.read.sensitive in addition to
    // notifications.notification.read. notifications.notification.admin is the
    // administrative override for dispatch (admin re-send).
    public const string NotificationRead = "notifications.notification.read";
    public const string NotificationReadSensitive = "notifications.notification.read.sensitive";
    public const string NotificationCreate = "notifications.notification.create";
    public const string NotificationSend = "notifications.notification.send";
    public const string NotificationAdmin = "notifications.notification.admin";
    public const string NotificationTypeManage = "notifications.type.manage";
    public const string NotificationTemplateRead = "notifications.template.read";
    public const string NotificationPreferenceManage = "notifications.preference.manage";

    public const string SearchResultRead = "search.result.read";
    public const string SearchResultReadSensitive = "search.result.read.sensitive";
    public const string SearchIndexManage = "search.index.manage";

    // Audit capabilities (audit.*, ratified ADR-027). Journal reads and the
    // sensitive second pass are separate capabilities; audit.entry.admin never
    // implies read access — hold placement additionally requires read-level
    // visibility of every target entry (docs/audit.md, decision 12).
    public const string AuditEntryRead = "audit.entry.read";
    public const string AuditEntryReadSensitive = "audit.entry.read.sensitive";
    public const string AuditEntryExport = "audit.entry.export";
    public const string AuditEntryAdmin = "audit.entry.admin";

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
        "documents.document.read",
        "documents.document.upload",
        "documents.document.delete",
        "correspondence.letter.create",
        "correspondence.letter.submit",
        "correspondence.letter.read"
    ];
}
