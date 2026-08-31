using CommunityOS.Audit.Application;
using CommunityOS.Audit.Domain;
using CommunityOS.Audit.Infrastructure.Integration;
using CommunityOS.Contracts.Authorization;
using CommunityOS.Contracts.Documents;
using CommunityOS.Contracts.Notifications;
using CommunityOS.Contracts.Organization;
using CommunityOS.Contracts.Records;
using CommunityOS.Contracts.Workflow;
using FluentAssertions;
using Xunit;
using Records = CommunityOS.Contracts.Records;

namespace CommunityOS.Audit.Tests.Application;

/// <summary>
/// Verifies every ratified first-gate ingest mapping: action code, resource
/// identity, actor/subject/scope extraction, allowlisted metadata only,
/// deterministic sensitivity, and hash stability (same fact → same hash;
/// restated fact with new OccurredOn → distinct entry).
/// </summary>
public sealed class IngestMappingTests
{
    private static readonly DateTime Now = new(2026, 8, 22, 10, 0, 0, DateTimeKind.Utc);

    // ---- Records ---------------------------------------------------------

    [Fact]
    public void RecordCreated_maps_identity_scope_and_metadata_but_drops_nothing_ratified()
    {
        var recordId = Guid.NewGuid();
        var subject = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var unit = Guid.NewGuid();

        var candidate = AuditEventMapper.Map(new RecordCreated(
            recordId, "membership", "Draft", "person", subject, unit, creator, Now));

        candidate.SourceService.Should().Be("records");
        candidate.Action.Should().Be("record-created");
        candidate.ResourceType.Should().Be("record");
        candidate.ResourceId.Should().Be(recordId);
        candidate.SubjectId.Should().Be(subject);
        candidate.ActorId.Should().Be(creator);
        candidate.OrganizationUnitId.Should().Be(unit);
        candidate.Sensitivity.Should().Be(AuditSensitivity.Normal);
        candidate.Metadata!.Values.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["category"] = "membership", ["status"] = "Draft", ["subject_type"] = "person"
        });
    }

    [Fact]
    public void Lifecycle_transitions_map_action_codes_and_optional_actors()
    {
        var recordId = Guid.NewGuid();
        var actor = Guid.NewGuid();

        AuditEventMapper.Map(new RecordSubmitted(recordId, "Submitted", Now))
            .Should().Match<IngestCandidate>(c => c.Action == "record-submitted"
                && c.Metadata!.Values["status"] == "Submitted" && c.ActorId == null);

        AuditEventMapper.Map(new RecordUnderReview(recordId, actor, Now))
            .Action.Should().Be("record-under-review");

        AuditEventMapper.Map(new RecordVerified(recordId, actor, Now))
            .ActorId.Should().Be(actor);

        AuditEventMapper.Map(new RecordRejected(recordId, actor, Now))
            .Action.Should().Be("record-rejected");
    }

    [Fact]
    public void RecordCorrected_maps_version_metadata_and_null_supersede_is_omitted()
    {
        var recordId = Guid.NewGuid();
        var correctedBy = Guid.NewGuid();

        var withSupersede = AuditEventMapper.Map(new RecordCorrected(recordId, 3, 2, correctedBy, Now));
        withSupersede.Metadata!.Values.Keys.Should().BeEquivalentTo(
            ["version_number", "supersedes_version_number"]);

        var withoutSupersede = AuditEventMapper.Map(new RecordCorrected(recordId, 4, null, correctedBy, Now));
        withoutSupersede.Metadata!.Values.Keys.Should().ContainSingle("version_number");
    }

    [Fact]
    public void RecordClassified_sensitivity_follows_the_payload_flag()
    {
        var recordId = Guid.NewGuid();

        AuditEventMapper.Map(new RecordClassified(recordId, "confidential", true, Now))
            .Sensitivity.Should().Be(AuditSensitivity.Sensitive);
        AuditEventMapper.Map(new RecordClassified(recordId, null, false, Now))
            .Sensitivity.Should().Be(AuditSensitivity.Normal);

        var noClassification = AuditEventMapper.Map(new RecordClassified(recordId, null, false, Now));
        noClassification.Metadata.Should().BeNull();

        var classified = AuditEventMapper.Map(new RecordClassified(recordId, "public", false, Now));
        classified.Metadata!.Values.Should().ContainKey("classification_code");
    }

    [Fact]
    public void Hold_events_are_sensitive_and_carry_hold_ids_as_secondary_resources()
    {
        var recordId = Guid.NewGuid();
        var holdId = Guid.NewGuid();
        var actor = Guid.NewGuid();

        var placed = AuditEventMapper.Map(new RecordHoldPlaced(holdId, recordId, "legal", actor, Now));
        placed.Sensitivity.Should().Be(AuditSensitivity.Sensitive);
        placed.SecondaryResourceId.Should().Be(holdId);
        placed.ActorId.Should().Be(actor);
        placed.Metadata!.Values.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["hold_id"] = holdId.ToString("D"), ["hold_type"] = "legal"
        });
        placed.Metadata.Values.Should().NotContainKey("reason",
            "hold reasons never enter the journal");

        var released = AuditEventMapper.Map(new RecordHoldReleased(holdId, recordId, "legal", actor, Now));
        released.Sensitivity.Should().Be(AuditSensitivity.Sensitive);
        released.Action.Should().Be("record-hold-released");
    }

    [Fact]
    public void Retention_and_evidence_events_map_secondary_resources_and_codes()
    {
        var recordId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var retentionChanged = AuditEventMapper.Map(new RecordRetentionChanged(recordId, "standard-7y", "P7Y", Now));
        retentionChanged.Action.Should().Be("record-retention-changed");
        retentionChanged.Metadata!.Values.Should().ContainKeys("retention_schedule_code", "retention_period");

        var attached = AuditEventMapper.Map(new RecordEvidenceAttached(recordId, documentId, 2, "annex", Now));
        attached.SecondaryResourceId.Should().Be(documentId);
        attached.Metadata!.Values["reference_type"].Should().Be("annex");

        var removed = AuditEventMapper.Map(new RecordEvidenceRemoved(recordId, documentId, 2, Now));
        removed.SecondaryResourceId.Should().Be(documentId);

        var expired = AuditEventMapper.Map(new RecordRetentionExpired(recordId, "standard-7y", Now.AddDays(-1), Now));
        expired.Action.Should().Be("record-retention-expired");
        expired.Metadata!.Values["retention_schedule_code"].Should().Be("standard-7y");
    }

    // ---- Workflow ----------------------------------------------------------

    [Fact]
    public void WorkflowTaskCreated_maps_domain_reference_definition_codes_and_scope()
    {
        var taskId = Guid.NewGuid();
        var domainEntityId = Guid.NewGuid();
        var unit = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        var candidate = AuditEventMapper.Map(new WorkflowTaskCreated(
            taskId, "membership-review", "record", domainEntityId, unit, createdBy, Now));

        candidate.SourceService.Should().Be("workflow");
        candidate.ResourceType.Should().Be("workflow-task");
        candidate.SecondaryResourceId.Should().Be(domainEntityId);
        candidate.OrganizationUnitId.Should().Be(unit);
        candidate.Metadata!.Values.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["definition_code"] = "membership-review", ["domain_type"] = "record"
        });
    }

    [Fact]
    public void WorkflowTaskAssignment_carries_counts_never_assignee_id_lists()
    {
        var taskId = Guid.NewGuid();
        var assignees = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var candidate = AuditEventMapper.Map(new WorkflowTaskAssigned(
            taskId, "review", assignees, Guid.NewGuid(), Now));

        candidate.Metadata!.Values["assignee_count"].Should().Be("2");
        candidate.Metadata.Json.Should().NotContain(assignees[0].ToString("D"),
            "multi-valued person id lists are not allowlisted metadata");
    }

    [Fact]
    public void WorkflowTaskCompleted_carries_the_outcome_code_on_the_entry()
    {
        var candidate = AuditEventMapper.Map(new WorkflowTaskCompleted(
            Guid.NewGuid(), "review", "approved", Guid.NewGuid(), Now));

        candidate.Outcome.Should().Be("approved");
        candidate.Action.Should().Be("workflow-task-completed");
    }

    [Fact]
    public void WorkflowTaskEscalation_carries_count_only()
    {
        var candidate = AuditEventMapper.Map(new WorkflowTaskEscalated(
            Guid.NewGuid(), "review", [Guid.NewGuid()], Guid.NewGuid(), Now));

        candidate.Action.Should().Be("workflow-task-escalated");
        candidate.Metadata!.Values["escalated_to_count"].Should().Be("1");
    }

    [Fact]
    public void WorkflowTaskCancelled_maps_actor()
    {
        var cancelledBy = Guid.NewGuid();
        var candidate = AuditEventMapper.Map(new WorkflowTaskCancelled(Guid.NewGuid(), "review", cancelledBy, Now));

        candidate.ActorId.Should().Be(cancelledBy);
        candidate.Action.Should().Be("workflow-task-cancelled");
    }

    // ---- Notifications -------------------------------------------------------

    [Fact]
    public void NotificationDispatched_persists_counts_only()
    {
        var notificationId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();

        var candidate = AuditEventMapper.Map(new NotificationDispatched(
            notificationId, "announcement", "email", "community-event", sourceId, 120, Now));

        candidate.SourceService.Should().Be("notifications");
        candidate.ResourceType.Should().Be("notification");
        candidate.SubjectId.Should().BeNull();
        candidate.ActorId.Should().BeNull();
        candidate.SecondaryResourceId.Should().Be(sourceId);
        candidate.Metadata!.Values.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["type_code"] = "announcement", ["channel"] = "email",
            ["source_type"] = "community-event", ["recipient_count"] = "120"
        });
    }

    // ---- Authorization -----------------------------------------------------

    [Fact]
    public void RoleAssigned_maps_assignment_identity_metadata_and_org_unit_tier_scope()
    {
        var assignmentId = Guid.NewGuid();
        var subject = Guid.NewGuid();
        var unit = Guid.NewGuid();

        var candidate = AuditEventMapper.Map(new RoleAssigned(
            assignmentId, subject, "treasurer", "Local", unit, DateTime.UtcNow, null, Now));

        candidate.SourceService.Should().Be("authorization");
        candidate.Action.Should().Be("role-assigned");
        candidate.ResourceType.Should().Be("authz-role");
        candidate.ResourceId.Should().Be(assignmentId);
        candidate.SubjectId.Should().Be(subject);
        candidate.ActorId.Should().BeNull();
        candidate.OrganizationUnitId.Should().Be(unit);
        candidate.Sensitivity.Should().Be(AuditSensitivity.Normal);
        candidate.Metadata!.Values.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["role_code"] = "treasurer", ["scope_type"] = "Local"
        });
    }

    [Fact]
    public void RoleAssigned_global_and_resource_scopes_do_not_fabricate_an_org_unit_scope()
    {
        var assignmentId = Guid.NewGuid();
        var subject = Guid.NewGuid();

        var global = AuditEventMapper.Map(new RoleAssigned(
            assignmentId, subject, "member", "Global", null, DateTime.UtcNow, null, Now));
        global.OrganizationUnitId.Should().BeNull();
        global.Metadata!.Values["scope_type"].Should().Be("Global");

        var resource = AuditEventMapper.Map(new RoleAssigned(
            assignmentId, subject, "editor", "Resource", Guid.NewGuid(), DateTime.UtcNow, null, Now));
        resource.OrganizationUnitId.Should().BeNull();
    }

    [Fact]
    public void RoleRevoked_maps_assignment_identity_and_role_code_without_fabricating_scope()
    {
        var assignmentId = Guid.NewGuid();
        var subject = Guid.NewGuid();

        var candidate = AuditEventMapper.Map(new RoleRevoked(assignmentId, subject, "treasurer", Now));

        candidate.Action.Should().Be("role-revoked");
        candidate.ResourceType.Should().Be("authz-role");
        candidate.ResourceId.Should().Be(assignmentId);
        candidate.SubjectId.Should().Be(subject);
        candidate.ActorId.Should().BeNull();
        candidate.OrganizationUnitId.Should().BeNull();
        candidate.Metadata!.Values.Should().ContainKey("role_code");
    }

    [Fact]
    public void Delegation_events_map_delegate_subject_and_delegator_actor_with_global_scope()
    {
        var delegationId = Guid.NewGuid();
        var delegator = Guid.NewGuid();
        var delegatee = Guid.NewGuid();

        var granted = AuditEventMapper.Map(new DelegationGranted(delegationId, delegator, delegatee, Now));
        granted.Action.Should().Be("delegation-granted");
        granted.ResourceType.Should().Be("authz-delegation");
        granted.ResourceId.Should().Be(delegationId);
        granted.SubjectId.Should().Be(delegatee);
        granted.ActorId.Should().Be(delegator);
        granted.OrganizationUnitId.Should().BeNull();
        granted.Metadata.Should().BeNull();

        var revoked = AuditEventMapper.Map(new DelegationRevoked(delegationId, delegator, delegatee, Now));
        revoked.Action.Should().Be("delegation-revoked");
        revoked.ResourceId.Should().Be(delegationId);
        revoked.SubjectId.Should().Be(delegatee);
        revoked.ActorId.Should().Be(delegator);
    }

    [Fact]
    public void Break_glass_events_map_request_identity_actors_and_are_sensitive()
    {
        var requestId = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var approver = Guid.NewGuid();
        var revokedBy = Guid.NewGuid();

        var requested = AuditEventMapper.Map(new BreakGlassRequested(requestId, requester, Now));
        requested.Action.Should().Be("break-glass-requested");
        requested.ResourceType.Should().Be("break-glass-request");
        requested.ResourceId.Should().Be(requestId);
        requested.SubjectId.Should().BeNull();
        requested.ActorId.Should().Be(requester);
        requested.OrganizationUnitId.Should().BeNull();
        requested.Sensitivity.Should().Be(AuditSensitivity.Sensitive);
        requested.Metadata.Should().BeNull();

        var approved = AuditEventMapper.Map(new BreakGlassApproved(
            requestId, requester, approver, DateTime.UtcNow.AddHours(1), Now));
        approved.Action.Should().Be("break-glass-approved");
        approved.ResourceId.Should().Be(requestId);
        approved.SubjectId.Should().BeNull();
        approved.ActorId.Should().Be(approver);
        approved.Sensitivity.Should().Be(AuditSensitivity.Sensitive);

        var revoked = AuditEventMapper.Map(new BreakGlassRevoked(requestId, requester, revokedBy, Now));
        revoked.Action.Should().Be("break-glass-revoked");
        revoked.ResourceId.Should().Be(requestId);
        revoked.SubjectId.Should().BeNull();
        revoked.ActorId.Should().Be(revokedBy);
        revoked.Sensitivity.Should().Be(AuditSensitivity.Sensitive);
    }

    // ---- Documents -------------------------------------------------------

    [Fact]
    public void DocumentClassified_maps_classification_and_is_sensitive_only_when_payload_flags_it()
    {
        var documentId = Guid.NewGuid();

        var sensitive = AuditEventMapper.Map(new DocumentClassified(documentId, "restricted", true, Now));
        sensitive.SourceService.Should().Be("documents");
        sensitive.Action.Should().Be("document-classified");
        sensitive.ResourceType.Should().Be("document");
        sensitive.ResourceId.Should().Be(documentId);
        sensitive.SubjectId.Should().BeNull();
        sensitive.ActorId.Should().BeNull();
        sensitive.OrganizationUnitId.Should().BeNull();
        sensitive.Sensitivity.Should().Be(AuditSensitivity.Sensitive);
        sensitive.Metadata!.Values.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["classification_code"] = "restricted"
        });

        var normal = AuditEventMapper.Map(new DocumentClassified(documentId, "public", false, Now));
        normal.Sensitivity.Should().Be(AuditSensitivity.Normal);
    }

    [Fact]
    public void DocumentClassified_with_null_code_omits_metadata_but_stays_normal()
    {
        var normal = AuditEventMapper.Map(new DocumentClassified(Guid.NewGuid(), null, false, Now));
        normal.Sensitivity.Should().Be(AuditSensitivity.Normal);
        normal.Metadata.Should().BeNull();
    }

    [Fact]
    public void DocumentDeactivated_maps_identity_without_fabricating_scope()
    {
        var documentId = Guid.NewGuid();

        var candidate = AuditEventMapper.Map(new DocumentDeactivated(documentId, Now));
        candidate.Action.Should().Be("document-deactivated");
        candidate.ResourceType.Should().Be("document");
        candidate.ResourceId.Should().Be(documentId);
        candidate.SubjectId.Should().BeNull();
        candidate.ActorId.Should().BeNull();
        candidate.OrganizationUnitId.Should().BeNull();
        candidate.Sensitivity.Should().Be(AuditSensitivity.Normal);
        candidate.Metadata.Should().BeNull();
    }

    [Fact]
    public void DocumentRestored_maps_identity_and_status_metadata()
    {
        var documentId = Guid.NewGuid();

        var candidate = AuditEventMapper.Map(new DocumentRestored(documentId, "Active", Now));
        candidate.Action.Should().Be("document-restored");
        candidate.ResourceType.Should().Be("document");
        candidate.ResourceId.Should().Be(documentId);
        candidate.SubjectId.Should().BeNull();
        candidate.ActorId.Should().BeNull();
        candidate.OrganizationUnitId.Should().BeNull();
        candidate.Sensitivity.Should().Be(AuditSensitivity.Normal);
        candidate.Metadata!.Values.Should().ContainKey("status");
        candidate.Metadata!.Values["status"].Should().Be("Active");
    }

    [Fact]
    public void DocumentContentDownloaded_maps_version_secondary_actor_and_is_sensitive()
    {
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var candidate = AuditEventMapper.Map(new DocumentContentDownloaded(documentId, versionId, actorId, Now));
        candidate.Action.Should().Be("document-content-downloaded");
        candidate.ResourceType.Should().Be("document");
        candidate.ResourceId.Should().Be(documentId);
        candidate.SecondaryResourceId.Should().Be(versionId);
        candidate.SubjectId.Should().BeNull();
        candidate.ActorId.Should().Be(actorId);
        candidate.OrganizationUnitId.Should().BeNull();
        candidate.Sensitivity.Should().Be(AuditSensitivity.Sensitive);
        candidate.Metadata.Should().BeNull();
    }

    [Fact]
    public void DocumentScanCompleted_maps_version_secondary_scan_status_as_outcome_and_is_normal()
    {
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var candidate = AuditEventMapper.Map(new DocumentScanCompleted(documentId, versionId, "Clean", Now));
        candidate.Action.Should().Be("document-scan-completed");
        candidate.ResourceType.Should().Be("document");
        candidate.ResourceId.Should().Be(documentId);
        candidate.SecondaryResourceId.Should().Be(versionId);
        candidate.SubjectId.Should().BeNull();
        candidate.ActorId.Should().BeNull();
        candidate.OrganizationUnitId.Should().BeNull();
        candidate.Sensitivity.Should().Be(AuditSensitivity.Normal);
        candidate.Outcome.Should().Be("Clean");
        candidate.Metadata.Should().BeNull();
    }

    // ---- Hash semantics --------------------------------------------------------

    [Fact]
    public void The_same_logical_event_hashes_identically_regardless_of_mapping_order()
    {
        var e1 = new RecordVerified(Guid.NewGuid(), Guid.NewGuid(), Now);
        var e2 = e1 with { };

        AuditEventMapper.Map(e1).SourceEventHash.Should().Be(AuditEventMapper.Map(e2).SourceEventHash);
    }

    [Fact]
    public void A_restated_fact_with_a_new_timestamp_is_a_distinct_entry()
    {
        var e1 = new RecordArchived(Guid.NewGuid(), Now);
        var restated = e1 with { OccurredOn = Now.AddSeconds(1) };

        AuditEventMapper.Map(e1).SourceEventHash.Should().NotBe(AuditEventMapper.Map(restated).SourceEventHash);
    }

    [Fact]
    public void Authorization_identical_fact_hashes_identically_and_restatement_is_distinct()
    {
        var e1 = new RoleAssigned(
            Guid.NewGuid(), Guid.NewGuid(), "treasurer", "Local", Guid.NewGuid(), DateTime.UtcNow, null, Now);
        var e2 = e1 with { };

        AuditEventMapper.Map(e1).SourceEventHash.Should().Be(AuditEventMapper.Map(e2).SourceEventHash);

        var restated = e1 with { OccurredOn = Now.AddSeconds(1) };
        AuditEventMapper.Map(e1).SourceEventHash.Should().NotBe(AuditEventMapper.Map(restated).SourceEventHash);
    }

    [Fact]
    public void Document_identical_fact_hashes_identically_and_occurrence_is_distinct()
    {
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var e1 = new DocumentScanCompleted(documentId, versionId, "Clean", Now);
        var e2 = e1 with { };

        AuditEventMapper.Map(e1).SourceEventHash.Should().Be(AuditEventMapper.Map(e2).SourceEventHash);

        var reScanned = e1 with { ScanStatus = "Rejected" };
        AuditEventMapper.Map(e1).SourceEventHash.Should().NotBe(AuditEventMapper.Map(reScanned).SourceEventHash);

        var restated = e1 with { OccurredOn = Now.AddSeconds(1) };
        AuditEventMapper.Map(e1).SourceEventHash.Should().NotBe(AuditEventMapper.Map(restated).SourceEventHash);
    }
}
