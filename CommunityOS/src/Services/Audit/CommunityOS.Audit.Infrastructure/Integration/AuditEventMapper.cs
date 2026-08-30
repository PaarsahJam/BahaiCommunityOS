using System.Globalization;
using CommunityOS.Audit.Application;
using CommunityOS.Audit.Domain;
using CommunityOS.Contracts.Authorization;
using CommunityOS.Contracts.Notifications;
using CommunityOS.Contracts.Records;
using CommunityOS.Contracts.Workflow;

namespace CommunityOS.Audit.Infrastructure.Integration;

/// <summary>
/// Pure, side-effect-free translation from producer payloads to
/// <see cref="IngestCandidate"/>s — the ratified first-gate ingest mappings
/// (docs/audit.md, ADR-027 decision 9). Every candidate carries:
/// action code, resource identity, optional actor/subject/scope, derived
/// sensitivity, allowlisted scalar metadata and a canonical source-event hash.
/// Payload fields outside the allowlist are dropped by construction.
/// </summary>
public static class AuditEventMapper
{
    // ---- Records -----------------------------------------------------------

    public static IngestCandidate Map(RecordCreated e) => new(
        AuditSources.Records, nameof(RecordCreated), "record-created",
        AuditSources.ResourceTypes.Record, e.RecordId,
        SecondaryResourceId: null,
        SubjectId: e.SubjectId,
        ActorId: e.CreatedBy,
        OrganizationUnitId: e.OrganizationUnitId,
        Sensitivity: AuditSensitivity.Normal,
        Outcome: null,
        Metadata: Build(("category", e.Category), ("status", e.Status), ("subject_type", e.SubjectType)),
        OccurredOn: e.OccurredOn,
        SourceEventHash: RecordsHash(nameof(RecordCreated), e.RecordId, null, e.OccurredOn,
            $"{e.Category}|{e.Status}|{e.SubjectType}|{e.SubjectId:D}"));

    public static IngestCandidate Map(RecordSubmitted e) => new(
        AuditSources.Records, nameof(RecordSubmitted), "record-submitted",
        AuditSources.ResourceTypes.Record, e.RecordId,
        null, null, null, null,
        AuditSensitivity.Normal, null,
        Build(("status", e.Status)),
        e.OccurredOn, RecordsHash(nameof(RecordSubmitted), e.RecordId, null, e.OccurredOn, e.Status));

    public static IngestCandidate Map(RecordUnderReview e) => new(
        AuditSources.Records, nameof(RecordUnderReview), "record-under-review",
        AuditSources.ResourceTypes.Record, e.RecordId,
        null, null, e.ReviewerId, null,
        AuditSensitivity.Normal, null, null,
        e.OccurredOn,
        RecordsHash(nameof(RecordUnderReview), e.RecordId, null, e.OccurredOn, $"{e.ReviewerId:D}"));

    public static IngestCandidate Map(RecordVerified e) => new(
        AuditSources.Records, nameof(RecordVerified), "record-verified",
        AuditSources.ResourceTypes.Record, e.RecordId,
        null, null, e.VerifiedBy, null,
        AuditSensitivity.Normal, null, null,
        e.OccurredOn,
        RecordsHash(nameof(RecordVerified), e.RecordId, null, e.OccurredOn, $"{e.VerifiedBy:D}"));

    public static IngestCandidate Map(RecordRejected e) => new(
        AuditSources.Records, nameof(RecordRejected), "record-rejected",
        AuditSources.ResourceTypes.Record, e.RecordId,
        null, null, e.RejectedBy, null,
        AuditSensitivity.Normal, null, null,
        e.OccurredOn,
        RecordsHash(nameof(RecordRejected), e.RecordId, null, e.OccurredOn, $"{e.RejectedBy:D}"));

    public static IngestCandidate Map(RecordCorrected e) => new(
        AuditSources.Records, nameof(RecordCorrected), "record-corrected",
        AuditSources.ResourceTypes.Record, e.RecordId,
        null, null, e.CorrectedBy, null,
        AuditSensitivity.Normal, null,
        Build(("version_number", e.VersionNumber), ("supersedes_version_number", e.SupersedesVersionNumber)),
        e.OccurredOn,
        RecordsHash(nameof(RecordCorrected), e.RecordId, null, e.OccurredOn,
            $"v{e.VersionNumber.ToString(CultureInfo.InvariantCulture)}|s{e.SupersedesVersionNumber?.ToString(CultureInfo.InvariantCulture) ?? "-"}|{e.CorrectedBy:D}"));

    public static IngestCandidate Map(RecordArchived e) => new(
        AuditSources.Records, nameof(RecordArchived), "record-archived",
        AuditSources.ResourceTypes.Record, e.RecordId,
        null, null, null, null,
        AuditSensitivity.Normal, null, null,
        e.OccurredOn, RecordsHash(nameof(RecordArchived), e.RecordId, null, e.OccurredOn, string.Empty));

    public static IngestCandidate Map(RecordDeactivated e) => new(
        AuditSources.Records, nameof(RecordDeactivated), "record-deactivated",
        AuditSources.ResourceTypes.Record, e.RecordId,
        null, null, null, null,
        AuditSensitivity.Normal, null, null,
        e.OccurredOn, RecordsHash(nameof(RecordDeactivated), e.RecordId, null, e.OccurredOn, string.Empty));

    public static IngestCandidate Map(RecordRestored e) => new(
        AuditSources.Records, nameof(RecordRestored), "record-restored",
        AuditSources.ResourceTypes.Record, e.RecordId,
        null, null, null, null,
        AuditSensitivity.Normal, null,
        Build(("status", e.Status)),
        e.OccurredOn, RecordsHash(nameof(RecordRestored), e.RecordId, null, e.OccurredOn, e.Status));

    public static IngestCandidate Map(RecordClassified e)
    {
        var sensitivity = SensitiveAuditEventTypes.Resolve(nameof(RecordClassified), e.IsSensitive);
        return new IngestCandidate(
            AuditSources.Records, nameof(RecordClassified), "record-classified",
            AuditSources.ResourceTypes.Record, e.RecordId,
            null, null, null, null,
            sensitivity, null,
            e.ClassificationCode is null ? null : Build(("classification_code", e.ClassificationCode)),
            e.OccurredOn,
            RecordsHash(nameof(RecordClassified), e.RecordId, null, e.OccurredOn,
                $"{e.ClassificationCode ?? "-"}|{e.IsSensitive}"));
    }

    public static IngestCandidate Map(RecordHoldPlaced e) => new(
        AuditSources.Records, nameof(RecordHoldPlaced), "record-hold-placed",
        AuditSources.ResourceTypes.Record, e.RecordId,
        SecondaryResourceId: e.HoldId,
        SubjectId: null,
        ActorId: e.PlacedBy,
        OrganizationUnitId: null,
        Sensitivity: SensitiveAuditEventTypes.Resolve(nameof(RecordHoldPlaced), payloadIsSensitive: false),
        Outcome: null,
        Metadata: Build(("hold_id", e.HoldId), ("hold_type", e.HoldType)),
        OccurredOn: e.OccurredOn,
        SourceEventHash: RecordsHash(nameof(RecordHoldPlaced), e.RecordId, e.HoldId, e.OccurredOn,
            $"{e.HoldType}"));

    public static IngestCandidate Map(RecordHoldReleased e) => new(
        AuditSources.Records, nameof(RecordHoldReleased), "record-hold-released",
        AuditSources.ResourceTypes.Record, e.RecordId,
        SecondaryResourceId: e.HoldId,
        SubjectId: null,
        ActorId: e.ReleasedBy,
        OrganizationUnitId: null,
        Sensitivity: SensitiveAuditEventTypes.Resolve(nameof(RecordHoldReleased), payloadIsSensitive: false),
        Outcome: null,
        Metadata: Build(("hold_id", e.HoldId), ("hold_type", e.HoldType)),
        OccurredOn: e.OccurredOn,
        SourceEventHash: RecordsHash(nameof(RecordHoldReleased), e.RecordId, e.HoldId, e.OccurredOn,
            $"{e.HoldType}"));

    public static IngestCandidate Map(RecordRetentionChanged e) => new(
        AuditSources.Records, nameof(RecordRetentionChanged), "record-retention-changed",
        AuditSources.ResourceTypes.Record, e.RecordId,
        null, null, null, null,
        AuditSensitivity.Normal, null,
        Build(("retention_schedule_code", e.RetentionScheduleCode), ("retention_period", e.RetentionPeriod)),
        e.OccurredOn,
        RecordsHash(nameof(RecordRetentionChanged), e.RecordId, null, e.OccurredOn,
            $"{e.RetentionScheduleCode ?? "-"}|{e.RetentionPeriod ?? "-"}"));

    public static IngestCandidate Map(RecordEvidenceAttached e) => new(
        AuditSources.Records, nameof(RecordEvidenceAttached), "record-evidence-attached",
        AuditSources.ResourceTypes.Record, e.RecordId,
        SecondaryResourceId: e.DocumentId,
        SubjectId: null, ActorId: null, OrganizationUnitId: null,
        AuditSensitivity.Normal, null,
        Build(("version_number", e.VersionNumber), ("reference_type", e.ReferenceType)),
        e.OccurredOn,
        RecordsHash(nameof(RecordEvidenceAttached), e.RecordId, e.DocumentId, e.OccurredOn,
            $"v{e.VersionNumber}|{e.ReferenceType}"));

    public static IngestCandidate Map(RecordEvidenceRemoved e) => new(
        AuditSources.Records, nameof(RecordEvidenceRemoved), "record-evidence-removed",
        AuditSources.ResourceTypes.Record, e.RecordId,
        SecondaryResourceId: e.DocumentId,
        SubjectId: null, ActorId: null, OrganizationUnitId: null,
        AuditSensitivity.Normal, null,
        Build(("version_number", e.VersionNumber)),
        e.OccurredOn,
        RecordsHash(nameof(RecordEvidenceRemoved), e.RecordId, e.DocumentId, e.OccurredOn,
            $"v{e.VersionNumber}"));

    public static IngestCandidate Map(RecordRetentionExpired e) => new(
        AuditSources.Records, nameof(RecordRetentionExpired), "record-retention-expired",
        AuditSources.ResourceTypes.Record, e.RecordId,
        null, null, null, null,
        AuditSensitivity.Normal, null,
        Build(("retention_schedule_code", e.RetentionScheduleCode)),
        e.OccurredOn,
        RecordsHash(nameof(RecordRetentionExpired), e.RecordId, null, e.OccurredOn,
            $"{e.RetentionScheduleCode}|{e.ExpiredOn.ToUniversalTime():O}"));

    // ---- Workflow ----------------------------------------------------------

    public static IngestCandidate Map(WorkflowTaskCreated e) => new(
        AuditSources.Workflow, nameof(WorkflowTaskCreated), "workflow-task-created",
        AuditSources.ResourceTypes.WorkflowTask, e.TaskId,
        SecondaryResourceId: e.DomainEntityId,
        SubjectId: null,
        ActorId: e.CreatedBy,
        OrganizationUnitId: e.OrganizationUnitId,
        Sensitivity: AuditSensitivity.Normal,
        Outcome: null,
        Metadata: Build(("definition_code", e.DefinitionCode), ("domain_type", e.DomainType)),
        OccurredOn: e.OccurredOn,
        SourceEventHash: WorkflowHash(nameof(WorkflowTaskCreated), e.TaskId, e.DomainEntityId, e.OccurredOn,
            $"{e.DefinitionCode}|{e.DomainType}"));

    public static IngestCandidate Map(WorkflowTaskAssigned e) => new(
        AuditSources.Workflow, nameof(WorkflowTaskAssigned), "workflow-task-assigned",
        AuditSources.ResourceTypes.WorkflowTask, e.TaskId,
        null, null, e.AssignedBy, null,
        AuditSensitivity.Normal, null,
        Build(("definition_code", e.DefinitionCode), ("assignee_count", e.AssigneeIds.Count)),
        e.OccurredOn,
        WorkflowHash(nameof(WorkflowTaskAssigned), e.TaskId, null, e.OccurredOn,
            $"{e.DefinitionCode}|n{e.AssigneeIds.Count}|{e.AssignedBy:D}"));

    public static IngestCandidate Map(WorkflowTaskCompleted e) => new(
        AuditSources.Workflow, nameof(WorkflowTaskCompleted), "workflow-task-completed",
        AuditSources.ResourceTypes.WorkflowTask, e.TaskId,
        null, null, e.CompletedBy, null,
        AuditSensitivity.Normal,
        Outcome: e.Outcome,
        Metadata: Build(("definition_code", e.DefinitionCode)),
        OccurredOn: e.OccurredOn,
        WorkflowHash(nameof(WorkflowTaskCompleted), e.TaskId, null, e.OccurredOn,
            $"{e.DefinitionCode}|{e.Outcome}|{e.CompletedBy:D}"));

    public static IngestCandidate Map(WorkflowTaskCancelled e) => new(
        AuditSources.Workflow, nameof(WorkflowTaskCancelled), "workflow-task-cancelled",
        AuditSources.ResourceTypes.WorkflowTask, e.TaskId,
        null, null, e.CancelledBy, null,
        AuditSensitivity.Normal, null,
        Build(("definition_code", e.DefinitionCode)),
        e.OccurredOn,
        WorkflowHash(nameof(WorkflowTaskCancelled), e.TaskId, null, e.OccurredOn,
            $"{e.DefinitionCode}|{e.CancelledBy:D}"));

    public static IngestCandidate Map(WorkflowTaskEscalated e) => new(
        AuditSources.Workflow, nameof(WorkflowTaskEscalated), "workflow-task-escalated",
        AuditSources.ResourceTypes.WorkflowTask, e.TaskId,
        null, null, e.EscalatedBy, null,
        AuditSensitivity.Normal, null,
        Build(("definition_code", e.DefinitionCode), ("escalated_to_count", e.EscalatedTo.Count)),
        e.OccurredOn,
        WorkflowHash(nameof(WorkflowTaskEscalated), e.TaskId, null, e.OccurredOn,
            $"{e.DefinitionCode}|n{e.EscalatedTo.Count}|{e.EscalatedBy:D}"));

    // ---- Notifications -----------------------------------------------------

    public static IngestCandidate Map(NotificationDispatched e) => new(
        AuditSources.Notifications, nameof(NotificationDispatched), "notification-dispatched",
        AuditSources.ResourceTypes.Notification, e.NotificationId,
        SecondaryResourceId: e.SourceId,
        SubjectId: null, ActorId: null, OrganizationUnitId: null,
        AuditSensitivity.Normal, null,
        Build(("type_code", e.TypeCode), ("channel", e.Channel), ("source_type", e.SourceType),
              ("recipient_count", e.RecipientCount)),
        e.OccurredOn,
        NotificationHash(nameof(NotificationDispatched), e.NotificationId, e.SourceId, e.OccurredOn,
            $"{e.TypeCode}|{e.Channel}|{e.SourceType}|n{e.RecipientCount}"));

    // ---- Authorization -----------------------------------------------------

    public static IngestCandidate Map(RoleAssigned e) => new(
        AuditSources.Authorization, nameof(RoleAssigned), "role-assigned",
        AuditSources.ResourceTypes.AuthzRole, e.AssignmentId,
        null, e.SubjectId, null, OrganizationScopeId(e.ScopeType, e.ScopeId),
        AuditSensitivity.Normal, null,
        Build(("role_code", e.RoleCode), ("scope_type", e.ScopeType)),
        e.OccurredOn,
        AuthorizationHash(nameof(RoleAssigned), AuditSources.ResourceTypes.AuthzRole, e.AssignmentId, null, e.OccurredOn,
            $"{e.RoleCode}|{e.ScopeType}|{(e.ScopeId?.ToString("D", CultureInfo.InvariantCulture) ?? "-")}|" +
            $"{e.EffectiveFrom:O}|{(e.EffectiveUntil?.ToString("O", CultureInfo.InvariantCulture) ?? "-")}"));

    public static IngestCandidate Map(RoleRevoked e) => new(
        AuditSources.Authorization, nameof(RoleRevoked), "role-revoked",
        AuditSources.ResourceTypes.AuthzRole, e.AssignmentId,
        null, e.SubjectId, null, null,
        AuditSensitivity.Normal, null,
        Build(("role_code", e.RoleCode)),
        e.OccurredOn,
        AuthorizationHash(nameof(RoleRevoked), AuditSources.ResourceTypes.AuthzRole, e.AssignmentId, null, e.OccurredOn,
            $"{e.RoleCode}|{e.SubjectId:D}"));

    public static IngestCandidate Map(DelegationGranted e) => new(
        AuditSources.Authorization, nameof(DelegationGranted), "delegation-granted",
        AuditSources.ResourceTypes.AuthzDelegation, e.DelegationId,
        null, e.DelegateId, e.DelegatorId, null,
        AuditSensitivity.Normal, null,
        null,
        e.OccurredOn,
        AuthorizationHash(nameof(DelegationGranted), AuditSources.ResourceTypes.AuthzDelegation, e.DelegationId, null, e.OccurredOn,
            $"{e.DelegatorId:D}|{e.DelegateId:D}"));

    public static IngestCandidate Map(DelegationRevoked e) => new(
        AuditSources.Authorization, nameof(DelegationRevoked), "delegation-revoked",
        AuditSources.ResourceTypes.AuthzDelegation, e.DelegationId,
        null, e.DelegateId, e.DelegatorId, null,
        AuditSensitivity.Normal, null,
        null,
        e.OccurredOn,
        AuthorizationHash(nameof(DelegationRevoked), AuditSources.ResourceTypes.AuthzDelegation, e.DelegationId, null, e.OccurredOn,
            $"{e.DelegatorId:D}|{e.DelegateId:D}"));

    public static IngestCandidate Map(BreakGlassRequested e) => new(
        AuditSources.Authorization, nameof(BreakGlassRequested), "break-glass-requested",
        AuditSources.ResourceTypes.BreakGlassRequest, e.RequestId,
        null, null, e.RequesterId, null,
        AuditSensitivity.Sensitive, null,
        null,
        e.OccurredOn,
        AuthorizationHash(nameof(BreakGlassRequested), AuditSources.ResourceTypes.BreakGlassRequest, e.RequestId, null, e.OccurredOn,
            $"{e.RequesterId:D}"));

    public static IngestCandidate Map(BreakGlassApproved e) => new(
        AuditSources.Authorization, nameof(BreakGlassApproved), "break-glass-approved",
        AuditSources.ResourceTypes.BreakGlassRequest, e.RequestId,
        null, null, e.ApproverId, null,
        AuditSensitivity.Sensitive, null,
        null,
        e.OccurredOn,
        AuthorizationHash(nameof(BreakGlassApproved), AuditSources.ResourceTypes.BreakGlassRequest, e.RequestId, null, e.OccurredOn,
            $"{e.RequesterId:D}|{e.ApproverId:D}|{e.ApprovedUntil:O}"));

    public static IngestCandidate Map(BreakGlassRevoked e) => new(
        AuditSources.Authorization, nameof(BreakGlassRevoked), "break-glass-revoked",
        AuditSources.ResourceTypes.BreakGlassRequest, e.RequestId,
        null, null, e.RevokedBy, null,
        AuditSensitivity.Sensitive, null,
        null,
        e.OccurredOn,
        AuthorizationHash(nameof(BreakGlassRevoked), AuditSources.ResourceTypes.BreakGlassRequest, e.RequestId, null, e.OccurredOn,
            $"{e.RequesterId:D}|{e.RevokedBy:D}"));

    // ---- Helpers -------------------------------------------------------------

    /// <summary>Canonical identity hash for a Records-sourced fact.</summary>
    private static string RecordsHash(string eventType, Guid recordId, Guid? secondary, DateTime occurredOn, string discriminator) =>
        SourceEventHash.Compute(AuditSources.Records, eventType, AuditSources.ResourceTypes.Record,
            recordId, secondary, occurredOn, discriminator);

    /// <summary>Canonical identity hash for a Workflow-sourced fact.</summary>
    private static string WorkflowHash(string eventType, Guid taskId, Guid? secondary, DateTime occurredOn, string discriminator) =>
        SourceEventHash.Compute(AuditSources.Workflow, eventType, AuditSources.ResourceTypes.WorkflowTask,
            taskId, secondary, occurredOn, discriminator);

    /// <summary>Canonical identity hash for a Notifications-sourced fact.</summary>
    private static string NotificationHash(string eventType, Guid notificationId, Guid? secondary, DateTime occurredOn, string discriminator) =>
        SourceEventHash.Compute(AuditSources.Notifications, eventType, AuditSources.ResourceTypes.Notification,
            notificationId, secondary, occurredOn, discriminator);

    /// <summary>Canonical identity hash for an Authorization-sourced fact.</summary>
    private static string AuthorizationHash(string eventType, string resourceType, Guid resourceId, Guid? secondary, DateTime occurredOn, string discriminator) =>
        SourceEventHash.Compute(AuditSources.Authorization, eventType, resourceType,
            resourceId, secondary, occurredOn, discriminator);

    /// <summary>Organization-unit tier scopes select the hierarchy scope id as the
    /// entry's organization scope; global and resource scopes do not.</summary>
    private static Guid? OrganizationScopeId(string scopeType, Guid? scopeId)
    {
        if (scopeId is null || scopeType is null)
        {
            return null;
        }

        return scopeType.Equals("National", StringComparison.OrdinalIgnoreCase)
            || scopeType.Equals("Regional", StringComparison.OrdinalIgnoreCase)
            || scopeType.Equals("Local", StringComparison.OrdinalIgnoreCase)
            || scopeType.Equals("OrganizationUnit", StringComparison.OrdinalIgnoreCase)
            || scopeType.Equals("Committee", StringComparison.OrdinalIgnoreCase)
            ? scopeId
            : null;
    }

    /// <summary>Builds allowlisted metadata, silently dropping absent values.</summary>
    private static AuditMetadata? Build(params (string Key, object? Value)[] entries)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var (key, value) in entries)
        {
            if (value is not null)
            {
                dict[key] = value;
            }
        }

        return dict.Count == 0 ? null : AuditMetadata.Create(dict);
    }
}
