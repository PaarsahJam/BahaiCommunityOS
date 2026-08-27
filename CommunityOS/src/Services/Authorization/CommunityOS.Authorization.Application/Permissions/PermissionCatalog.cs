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

    // Correspondence capabilities (correspondence.*, ratified ADR-028
    // decision 15). Exact-match membership only and no capability implies
    // another — correspondence.letter.admin never implies read, and hold
    // placement additionally requires read-level visibility of every target
    // letter (docs/correspondence.md).
    public const string CorrespondenceLetterRead = "correspondence.letter.read";
    public const string CorrespondenceLetterReadSensitive = "correspondence.letter.read.sensitive";
    public const string CorrespondenceLetterCreate = "correspondence.letter.create";
    public const string CorrespondenceLetterUpdate = "correspondence.letter.update";
    public const string CorrespondenceLetterSubmit = "correspondence.letter.submit";
    public const string CorrespondenceLetterCancel = "correspondence.letter.cancel";
    public const string CorrespondenceLetterExport = "correspondence.letter.export";
    public const string CorrespondenceLetterAdmin = "correspondence.letter.admin";
    public const string CorrespondenceTemplateRead = "correspondence.template.read";
    public const string CorrespondenceTemplateManage = "correspondence.template.manage";

    // Localization capabilities (localization.*, ratified ADR-029 decision
    // 11). Exactly five capabilities; exact-match membership only and no
    // capability implies another — localization.locale.manage never implies
    // read, and review never implies propose. Institution-independent: the
    // catalog is shared across units (docs/localization.md).
    public const string LocalizationLocaleRead = "localization.locale.read";
    public const string LocalizationLocaleManage = "localization.locale.manage";
    public const string LocalizationResourceRead = "localization.resource.read";
    public const string LocalizationResourcePropose = "localization.resource.propose";
    public const string LocalizationResourceReview = "localization.resource.review";

    // AI Platform capabilities (ai.*, ratified ADR-030 decision 10). Exactly
    // two capabilities; exact-match membership only; manage does NOT imply
    // invoke. Registered at the implementation gate (Prompt 16K).
    public const string AiAssistInvoke = "ai.assist.invoke";
    public const string AiPlatformManage = "ai.platform.manage";

    // Finance capabilities (finance.*, ratified ADR-032 decision 12). Exactly
    // six baseline capabilities and no more; every capability is evaluated at
    // its fund's organizational scope and no capability implies another — a
    // finance.transaction.admin grant never implies record or approve, and
    // fund.manage never implies transaction.record. Deferred capabilities
    // (confidential attribution, wallet references, category, document
    // references, budget management) are NOT registered. Registered at the
    // implementation gate (Prompt 16U).
    public const string FinanceFundRead = "finance.fund.read";
    public const string FinanceFundManage = "finance.fund.manage";
    public const string FinanceTransactionRead = "finance.transaction.read";
    public const string FinanceTransactionRecord = "finance.transaction.record";
    public const string FinanceTransactionApprove = "finance.transaction.approve";
    public const string FinanceTransactionAdmin = "finance.transaction.admin";

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
