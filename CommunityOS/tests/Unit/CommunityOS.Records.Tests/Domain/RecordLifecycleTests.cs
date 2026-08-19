using CommunityOS.Records.Domain.Aggregates;
using CommunityOS.Records.Domain.Enumerations;
using CommunityOS.Records.Domain.Exceptions;
using CommunityOS.Records.Domain.Events;

namespace CommunityOS.Records.Tests.Domain;

/// <summary>
/// Domain-invariant tests for the Record aggregate (ADR-023): the guarded
/// lifecycle <c>Draft → Submitted → Under Review → Verified → Archived |
/// Deactivated</c> plus <c>Rejected</c>, separation of duties (the creator can
/// never verify/reject the record), the immutable authoritative version frozen
/// at verification and never mutated in place (corrections append superseding
/// versions), holds that block deactivation unless an administrative override
/// with a reason is used, retention expiry that only flags review, and
/// idempotent evidence attachment.
/// </summary>
public class RecordLifecycleTests
{
    private static readonly Guid Creator = Guid.NewGuid();
    private static readonly Guid Reviewer = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static RecordFieldValue Field(string key, string value, bool sensitive = false) =>
        RecordFieldValue.Create(key, value, sensitive);

    private static Record CreateRecord(params RecordFieldValue[] fields) =>
        Record.Create("membership", RecordSubjectTypes.Person, Guid.NewGuid(), Guid.NewGuid(),
            fields, isSensitive: false, Creator, Now);

    // --- Lifecycle ---

    [Fact]
    public void Create_starts_as_Draft_and_raises_RecordCreated()
    {
        var record = CreateRecord(Field("name", "A Flower"));

        record.Status.Should().Be(RecordStatus.Draft);
        record.Category.Should().Be("membership");
        record.SubjectType.Should().Be(RecordSubjectTypes.Person);
        record.WorkingFields.Should().Contain(f => f.FieldKey == "name");
        record.DomainEvents.Should().ContainSingle(e => e is RecordCreatedEvent);
    }

    [Fact]
    public void Submit_then_MoveUnderReview_walks_the_lifecycle()
    {
        var record = CreateRecord();

        record.Submit(Creator, Now);
        record.Status.Should().Be(RecordStatus.Submitted);

        record.MoveUnderReview(Reviewer, Now);
        record.Status.Should().Be(RecordStatus.UnderReview);

        record.Verify(Reviewer, Now);
        record.Status.Should().Be(RecordStatus.Verified);
        record.CurrentVersion.Should().NotBeNull();
        record.CurrentVersion!.VersionNumber.Should().Be(1);
        record.DomainEvents.Should().ContainSingle(e => e is RecordVerifiedEvent);
    }

    [Fact]
    public void Creator_cannot_move_record_into_under_review()
    {
        var record = CreateRecord();
        record.Submit(Creator, Now);

        var act = () => record.MoveUnderReview(Creator, Now);

        act.Should().Throw<CreatorVerificationConflictException>();
    }

    [Fact]
    public void Creator_cannot_verify_the_record()
    {
        var record = CreateRecord();
        record.Submit(Creator, Now);
        record.MoveUnderReview(Reviewer, Now);

        var act = () => record.Verify(Creator, Now);

        act.Should().Throw<CreatorVerificationConflictException>();
    }

    [Fact]
    public void Creator_cannot_reject_the_record()
    {
        var record = CreateRecord();
        record.Submit(Creator, Now);
        record.MoveUnderReview(Reviewer, Now);

        var act = () => record.Reject(Creator, Now);

        act.Should().Throw<CreatorVerificationConflictException>();
    }

    [Fact]
    public void Rejected_record_is_editable_but_cannot_be_resubmitted()
    {
        var record = CreateRecord();
        record.Submit(Creator, Now);
        record.MoveUnderReview(Reviewer, Now);
        record.Reject(Reviewer, Now);

        record.Status.Should().Be(RecordStatus.Rejected);

        // Fields may still be corrected after rejection...
        record.UpdateFields([Field("name", "Corrected")], Creator, Now);
        record.WorkingFields.Should().Contain(f => f.FieldKey == "name" && f.FieldValue == "Corrected");

        // ...but Rejected is a ratified terminal state: no resubmit path exists.
        var act = () => record.Submit(Creator, Now);
        act.Should().Throw<InvalidRecordTransitionException>();
    }

    [Fact]
    public void Verify_freezes_the_working_field_set_into_the_authoritative_version()
    {
        var record = CreateRecord(Field("name", "A Flower"));
        record.Submit(Creator, Now);
        record.MoveUnderReview(Reviewer, Now);
        record.Verify(Reviewer, Now);

        record.CurrentVersion!.Fields.Should().Contain(f => f.FieldKey == "name" && f.FieldValue == "A Flower");
        record.CurrentVersion.SupersedesVersionNumber.Should().BeNull();
    }

    [Fact]
    public void Verified_record_rejects_in_place_field_updates()
    {
        var record = CreateRecord();
        record.Submit(Creator, Now);
        record.MoveUnderReview(Reviewer, Now);
        record.Verify(Reviewer, Now);

        var act = () => record.UpdateFields([Field("name", "Changed")], Actor, Now);

        act.Should().Throw<VerifiedRecordFieldUpdateException>();
    }

    // --- Corrections (append-only versions) ---

    [Fact]
    public void Correct_appends_a_superseding_version_and_never_mutates_the_baseline()
    {
        var record = CreateRecord(Field("name", "A Flower"));
        record.Submit(Creator, Now);
        record.MoveUnderReview(Reviewer, Now);
        record.Verify(Reviewer, Now);

        var baselineId = record.CurrentVersion!.Id;
        record.Correct([Field("name", "A Flower"), Field("address", "New Address")],
            "Address corrected after verification.", Actor, Now);

        record.CurrentVersion!.VersionNumber.Should().Be(2);
        record.CurrentVersion.SupersedesVersionNumber.Should().Be(1);
        record.Versions.Should().HaveCount(2);
        record.Versions[0].Id.Should().Be(baselineId);
        record.Versions[0].Fields.Should().ContainSingle(f => f.FieldKey == "name");
        record.DomainEvents.Should().ContainSingle(e => e is RecordCorrectedEvent);
    }

    [Fact]
    public void Correct_requires_a_change_reason()
    {
        var record = CreateRecord();
        record.Submit(Creator, Now);
        record.MoveUnderReview(Reviewer, Now);
        record.Verify(Reviewer, Now);

        var act = () => record.Correct([Field("name", "x")], "  ", Actor, Now);

        act.Should().Throw<CorrectionChangeReasonRequiredException>();
    }

    [Fact]
    public void Correct_requires_the_record_to_be_verified()
    {
        var record = CreateRecord();

        var act = () => record.Correct([Field("name", "x")], "reason", Actor, Now);

        act.Should().Throw<InvalidRecordTransitionException>();
    }

    // --- Archive / Deactivate / Restore ---

    [Fact]
    public void Archive_moves_a_verified_record_to_archived()
    {
        var record = CreateRecord();
        record.Submit(Creator, Now);
        record.MoveUnderReview(Reviewer, Now);
        record.Verify(Reviewer, Now);

        record.Archive(Actor, Now);

        record.Status.Should().Be(RecordStatus.Archived);
    }

    [Fact]
    public void Deactivation_is_blocked_by_an_active_hold_without_admin_override()
    {
        var record = CreateRecord();
        record.Submit(Creator, Now);
        record.MoveUnderReview(Reviewer, Now);
        record.Verify(Reviewer, Now);
        record.PlaceHold("legal", "Legal matter under review.", null, Actor, Now);

        var act = () => record.Deactivate(Actor, adminOverride: false, null, Now);

        act.Should().Throw<HeldRecordDeactivationException>();
    }

    [Fact]
    public void Deactivation_with_admin_override_requires_a_reason()
    {
        var record = CreateRecord();
        record.Submit(Creator, Now);
        record.MoveUnderReview(Reviewer, Now);
        record.Verify(Reviewer, Now);
        record.PlaceHold("legal", "Legal matter under review.", null, Actor, Now);

        var act = () => record.Deactivate(Actor, adminOverride: true, null, Now);

        act.Should().Throw<AdminOverrideReasonRequiredException>();
    }

    [Fact]
    public void Deactivation_with_admin_override_and_reason_succeeds()
    {
        var record = CreateRecord();
        record.Submit(Creator, Now);
        record.MoveUnderReview(Reviewer, Now);
        record.Verify(Reviewer, Now);
        record.PlaceHold("legal", "Legal matter under review.", null, Actor, Now);

        record.Deactivate(Actor, adminOverride: true, "Approved by the national office.", Now);

        record.Status.Should().Be(RecordStatus.Deactivated);
    }

    [Fact]
    public void Restore_returns_deactivated_record_to_verified()
    {
        var record = CreateRecord();
        record.Submit(Creator, Now);
        record.MoveUnderReview(Reviewer, Now);
        record.Verify(Reviewer, Now);
        record.Deactivate(Actor, adminOverride: false, null, Now);

        record.Restore(Actor, Now);

        record.Status.Should().Be(RecordStatus.Verified);
        record.DomainEvents.Should().ContainSingle(e => e is RecordRestoredEvent);
    }

    // --- Classification / retention ---

    [Fact]
    public void SetClassification_raises_RecordClassified()
    {
        var record = CreateRecord();

        record.SetClassification("sensitive-personal", true, "ret-sched", Actor, Now);

        record.Classification.IsSensitive.Should().BeTrue();
        record.Classification.ClassificationCode.Should().Be("sensitive-personal");
        record.Classification.RetentionScheduleCode.Should().Be("ret-sched");
        record.DomainEvents.Should().ContainSingle(e => e is RecordClassifiedEvent);
    }

    [Fact]
    public void SetClassification_raises_RecordRetentionChanged_when_schedule_changes()
    {
        var record = CreateRecord();
        record.SetClassification(null, false, "schedule-a", Actor, Now);

        record.SetClassification(null, false, "schedule-b", Actor, Now);

        record.DomainEvents.OfType<RecordRetentionChangedEvent>().Should().HaveCount(2);
    }

    [Fact]
    public void FlagRetentionExpired_requires_a_schedule_and_never_destroys_data()
    {
        var record = CreateRecord();

        var act = () => record.FlagRetentionExpired(Now);
        act.Should().Throw<RetentionScheduleNotFoundException>();

        record.SetClassification(null, false, "ret-sched", Actor, Now);
        record.FlagRetentionExpired(Now);

        record.Classification.RetentionExpiredOn.Should().Be(Now);
        record.Status.Should().Be(RecordStatus.Draft); // data preserved
        record.DomainEvents.Should().ContainSingle(e => e is RecordRetentionExpiredEvent);
    }

    // --- Evidence ---

    [Fact]
    public void AttachEvidence_is_idempotent_per_document_version_type()
    {
        var record = CreateRecord();
        var docId = Guid.NewGuid();

        var first = record.AttachEvidence(docId, 1, "proof_of_address", Actor, Now);
        var second = record.AttachEvidence(docId, 1, "proof_of_address", Actor, Now);

        second.Id.Should().Be(first.Id);
        record.Evidence.Should().HaveCount(1);
        record.Evidence.Single().ReferenceType.Should().Be("proof_of_address");
    }

    [Fact]
    public void RemoveEvidence_raises_RecordEvidenceRemoved()
    {
        var record = CreateRecord();
        var evidence = record.AttachEvidence(Guid.NewGuid(), 1, "proof_of_address", Actor, Now);

        record.RemoveEvidence(evidence.Id, Actor, Now);

        record.Evidence.Should().BeEmpty();
        record.DomainEvents.Should().ContainSingle(e => e is RecordEvidenceRemovedEvent);
    }

    [Fact]
    public void RemoveEvidence_throws_when_reference_is_unknown()
    {
        var record = CreateRecord();

        var act = () => record.RemoveEvidence(Guid.NewGuid(), Actor, Now);

        act.Should().Throw<RecordEvidenceNotFoundException>();
    }

    // --- Holds ---

    [Fact]
    public void PlaceHold_records_the_reason_and_raises_RecordHoldPlaced()
    {
        var record = CreateRecord();

        var hold = record.PlaceHold("legal", "Subpoena received.", null, Actor, Now);

        hold.HoldType.Should().Be("legal");
        hold.IsActive.Should().BeTrue();
        record.HasActiveHold.Should().BeTrue();
        record.DomainEvents.Should().ContainSingle(e => e is RecordHoldPlacedEvent);
    }

    [Fact]
    public void PlaceHold_rejects_hold_types_outside_legal_and_administrative()
    {
        var record = CreateRecord();

        var act = () => record.PlaceHold("seizure", "Not a ratified hold type.", null, Actor, Now);

        act.Should().Throw<InvalidRecordHoldTypeException>();
    }

    [Fact]
    public void Hold_cannot_be_released_by_the_subject_that_placed_it()
    {
        var record = CreateRecord();
        var hold = record.PlaceHold("legal", "Subpoena received.", null, Actor, Now);

        var act = () => record.ReleaseHold(hold.Id, Actor, Now);

        act.Should().Throw<HoldReleaseByPlacerException>();
    }

    [Fact]
    public void Hold_can_be_released_by_a_different_subject()
    {
        var record = CreateRecord();
        var hold = record.PlaceHold("legal", "Subpoena received.", null, Actor, Now);

        record.ReleaseHold(hold.Id, Reviewer, Now);

        hold.IsActive.Should().BeFalse();
        record.HasActiveHold.Should().BeFalse();
        record.DomainEvents.Should().ContainSingle(e => e is RecordHoldReleasedEvent);
    }

    [Fact]
    public void ReleaseHold_throws_when_hold_is_unknown()
    {
        var record = CreateRecord();

        var act = () => record.ReleaseHold(Guid.NewGuid(), Reviewer, Now);

        act.Should().Throw<RecordHoldNotFoundException>();
    }

    // --- Scopes ---

    [Fact]
    public void AddOrganizationScope_is_idempotent_and_metadata_only()
    {
        var record = CreateRecord();
        var unitId = Guid.NewGuid();

        record.AddOrganizationScope(unitId, Actor, Now);
        record.AddOrganizationScope(unitId, Actor, Now);

        record.Scopes.Should().ContainSingle(s => s.OrganizationUnitId == unitId);
    }

    [Fact]
    public void RemoveOrganizationScope_removes_only_an_existing_scope()
    {
        var record = CreateRecord();
        var unitId = Guid.NewGuid();
        record.AddOrganizationScope(unitId, Actor, Now);

        record.RemoveOrganizationScope(unitId, Actor, Now);
        record.RemoveOrganizationScope(unitId, Actor, Now);

        record.Scopes.Should().BeEmpty();
    }
}