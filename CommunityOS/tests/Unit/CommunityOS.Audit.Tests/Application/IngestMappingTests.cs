using CommunityOS.Audit.Application;
using CommunityOS.Audit.Domain;
using CommunityOS.Audit.Infrastructure.Integration;
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
}
