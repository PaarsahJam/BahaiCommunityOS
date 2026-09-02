namespace CommunityOS.Audit.Domain;

/// <summary>
/// Stable vocabulary for audit entry provenance (ADR-027). Source services
/// and resource types are short codes persisted on every entry.
/// </summary>
public static class AuditSources
{
    public const string Records = "records";
    public const string Workflow = "workflow";
    public const string Notifications = "notifications";
    public const string Documents = "documents";
    public const string Authorization = "authorization";
    public const string Identity = "identity";
    public const string Audit = "audit";

    /// <summary>Producers whose compliance events persist at the first gate.</summary>
    public static readonly IReadOnlySet<string> FirstGateServices =
        new HashSet<string>(StringComparer.Ordinal) { Records, Workflow, Notifications };

    public static class ResourceTypes
    {
        public const string Record = "record";
        public const string WorkflowTask = "workflow-task";
        public const string Notification = "notification";
        public const string Document = "document";
        public const string AuthzRole = "authz-role";
        public const string AuthzDelegation = "authz-delegation";
        public const string BreakGlassRequest = "break-glass-request";
        public const string UserAccount = "user-account";
        public const string AuditEntry = "audit-entry";
        public const string AuditHold = "audit-entry-hold";
        public const string AuditExport = "audit-export";

        /// <summary>Resource types emitted by the ratified ingest mappings.</summary>
        public static readonly IReadOnlySet<string> All =
            new HashSet<string>(StringComparer.Ordinal)
            {
                Record, WorkflowTask, Notification, Document, AuthzRole, AuthzDelegation,
                BreakGlassRequest, UserAccount, AuditEntry, AuditHold, AuditExport
            };
    }
}

/// <summary>
/// Ratified sensitive-event list (ADR-027 decision 6): these event types are
/// always classified <see cref="AuditSensitivity.Sensitive"/> regardless of
/// payload. Producers may additionally flag individual events through the
/// payload <c>IsSensitive</c> gate (<c>RecordClassified</c>,
/// <c>DocumentClassified</c>).
/// </summary>
public static class SensitiveAuditEventTypes
{
    public const string BreakGlassRequested = "BreakGlassRequested";
    public const string BreakGlassApproved = "BreakGlassApproved";
    public const string BreakGlassRevoked = "BreakGlassRevoked";
    public const string RecordHoldPlaced = "RecordHoldPlaced";
    public const string RecordHoldReleased = "RecordHoldReleased";
    public const string DocumentContentDownloaded = "DocumentContentDownloaded";

    /// <summary>The ratified six-event sensitivity list.</summary>
    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal)
        {
            BreakGlassRequested, BreakGlassApproved, BreakGlassRevoked,
            RecordHoldPlaced, RecordHoldReleased, DocumentContentDownloaded
        };

    /// <summary>Deterministic sensitivity rule: list membership OR explicit
    /// payload flag.</summary>
    public static AuditSensitivity Resolve(string sourceEventType, bool payloadIsSensitive) =>
        All.Contains(sourceEventType) || payloadIsSensitive
            ? AuditSensitivity.Sensitive
            : AuditSensitivity.Normal;
}
