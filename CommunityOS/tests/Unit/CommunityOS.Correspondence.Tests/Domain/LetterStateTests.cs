using CommunityOS.Correspondence.Domain;
using CommunityOS.Correspondence.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace CommunityOS.Correspondence.Tests.Domain;

public sealed class LetterLifecycleTests
{
    private static readonly Guid Unit = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    private static Letter NewDraft() => Letter.CreateDraft(
        Unit, "general", "Subject line", "Body text",
        LetterSensitivity.Normal, Guid.NewGuid(), DateTime.UtcNow);

    private static Letter ConfirmedLetter()
    {
        var letter = NewDraft();
        letter.AddRecipient(RecipientKind.Person, Guid.NewGuid(), null, null, DateTime.UtcNow);
        letter.Confirm(Actor, DateTime.UtcNow);
        return letter;
    }

    [Fact]
    public void Draft_creation_sets_revision_one_and_appends_history()
    {
        var letter = NewDraft();
        letter.Status.Should().Be(LetterStatus.Draft);
        letter.Revision.Should().Be(1);
        letter.DrainPendingHistory().Should().ContainSingle(h => h.ToStatus == LetterStatus.Draft);
    }

    [Fact]
    public void Confirm_requires_subject_body_and_at_least_one_recipient()
    {
        var empty = NewDraft();
        var act = () => empty.Confirm(Actor, DateTime.UtcNow);
        act.Should().Throw<LetterConflictException>();

        var subjectOnly = Letter.CreateDraft(Unit, "general", "S", "", LetterSensitivity.Normal, Guid.NewGuid(), DateTime.UtcNow);
        subjectOnly.AddRecipient(RecipientKind.Person, Guid.NewGuid(), null, null, DateTime.UtcNow);
        ((Action)(() => subjectOnly.Confirm(Actor, DateTime.UtcNow))).Should().Throw<LetterConflictException>();

        var withRecipient = NewDraft();
        withRecipient.AddRecipient(RecipientKind.Unit, null, Guid.NewGuid(), null, DateTime.UtcNow);
        withRecipient.Confirm(Actor, DateTime.UtcNow);
        withRecipient.Status.Should().Be(LetterStatus.Confirmed);
    }

    [Fact]
    public void Unconfirm_returns_a_confirmed_letter_to_draft()
    {
        var letter = ConfirmedLetter();
        letter.Unconfirm(Actor, DateTime.UtcNow);
        letter.Status.Should().Be(LetterStatus.Draft);

        var act = () => letter.Unconfirm(Actor, DateTime.UtcNow);
        act.Should().Throw<LetterConflictException>();
    }

    [Fact]
    public void Submit_allocates_the_reference_and_stamps_retention()
    {
        var letter = ConfirmedLetter();
        var expires = new DateTime(2033, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        letter.Submit(Actor, 2026, 42, "institutional-5y", expires, DateTime.UtcNow);

        letter.Status.Should().Be(LetterStatus.Submitted);
        letter.LetterYear.Should().Be(2026);
        letter.LetterSequence.Should().Be(42);
        letter.ReferenceNumber.Should().Be("2026-00042");
        letter.RetentionClass.Should().Be("institutional-5y");
        letter.RetentionExpiresOn.Should().Be(expires);
        letter.SubmittedBy.Should().Be(Actor);
    }

    [Fact]
    public void Submit_rejects_non_positive_sequences()
    {
        var letter = ConfirmedLetter();
        var act = () => letter.Submit(Actor, 2026, 0, "default", null, DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Materialization_is_idempotent_per_document()
    {
        var letter = ConfirmedLetter();
        letter.Submit(Actor, 2026, 1, "default", null, DateTime.UtcNow);

        var documentId = Guid.NewGuid();
        letter.MarkMaterialized(documentId, 1, "hash", DateTime.UtcNow);
        letter.Status.Should().Be(LetterStatus.Materialized);

        // Same correlation replays harmlessly; a different document id from
        // Submitted status is illegal.
        letter.MarkMaterialized(documentId, 1, "hash", DateTime.UtcNow);
        letter.DocumentLinks.Should().ContainSingle();

        var other = ConfirmedLetter();
        other.Submit(Actor, 2026, 2, "default", null, DateTime.UtcNow);
        ((Action)(() => other.MarkMaterialized(Guid.NewGuid(), 1, "hash", DateTime.UtcNow.AddMinutes(1))))
            .Should().NotThrow();
        ((Action)(() => other.MarkMaterialized(Guid.NewGuid(), 1, "hash", DateTime.UtcNow.AddMinutes(2))))
            .Should().Throw<LetterConflictException>();
    }

    [Fact]
    public void Dispatch_requires_materialization_then_delivery_outcomes()
    {
        var letter = ConfirmedLetter();
        letter.Submit(Actor, 2026, 3, "default", null, DateTime.UtcNow);
        ((Action)(() => letter.RecordDispatch("manual", Actor, DateTime.UtcNow)))
            .Should().Throw<LetterConflictException>("dispatch stays blocked until materialization");

        letter.MarkMaterialized(Guid.NewGuid(), 1, "hash", DateTime.UtcNow);
        letter.RecordDispatch("manual", Actor, DateTime.UtcNow);
        letter.Status.Should().Be(LetterStatus.Dispatched);
        letter.LastDispatchMethodCode.Should().Be("manual");

        letter.ConfirmDelivery("manual", Actor, DateTime.UtcNow);
        letter.Status.Should().Be(LetterStatus.Delivered);
        ((Action)(() => letter.FailDelivery("manual", "bad-address", Actor, DateTime.UtcNow)))
            .Should().Throw<LetterConflictException>("delivered is terminal for delivery outcomes");

        var dispatched = ConfirmedLetter();
        dispatched.Submit(Actor, 2026, 4, "default", null, DateTime.UtcNow);
        dispatched.MarkMaterialized(Guid.NewGuid(), 1, "hash", DateTime.UtcNow);
        dispatched.RecordDispatch("manual", Actor, DateTime.UtcNow);
        ((Action)(() => dispatched.FailDelivery("manual", "not-a-code", Actor, DateTime.UtcNow)))
            .Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(LetterStatus.Draft)]
    [InlineData(LetterStatus.Confirmed)]
    [InlineData(LetterStatus.Submitted)]
    public void Cancel_is_legal_only_pre_dispatch(LetterStatus from)
    {
        var letter = from == LetterStatus.Draft ? NewDraft() : ConfirmedLetter();
        if (from == LetterStatus.Submitted)
        {
            letter.Submit(Actor, 2026, 9, "default", null, DateTime.UtcNow);
        }

        letter.Cancel(Actor, "superseded", DateTime.UtcNow);
        letter.Status.Should().Be(LetterStatus.Cancelled);
        // The consumed reference is retained permanently.
        if (from == LetterStatus.Submitted)
        {
            letter.LetterSequence.Should().Be(9);
        }
    }

    [Fact]
    public void Cancel_rejects_post_dispatch_states()
    {
        // Materialized is still pre-dispatch: cancellation remains legal.
        var materialized = ConfirmedLetter();
        materialized.Submit(Actor, 2026, 5, "default", null, DateTime.UtcNow);
        materialized.MarkMaterialized(Guid.NewGuid(), 1, "hash", DateTime.UtcNow);
        materialized.Cancel(Actor, "superseded", DateTime.UtcNow);
        materialized.Status.Should().Be(LetterStatus.Cancelled);

        var dispatched = ConfirmedLetter();
        dispatched.Submit(Actor, 2026, 6, "default", null, DateTime.UtcNow);
        dispatched.MarkMaterialized(Guid.NewGuid(), 1, "hash", DateTime.UtcNow);
        dispatched.RecordDispatch("manual", Actor, DateTime.UtcNow);
        ((Action)(() => dispatched.Cancel(Actor, "superseded", DateTime.UtcNow)))
            .Should().Throw<LetterConflictException>();

        var delivered = ConfirmedLetter();
        delivered.Submit(Actor, 2026, 7, "default", null, DateTime.UtcNow);
        delivered.MarkMaterialized(Guid.NewGuid(), 1, "hash", DateTime.UtcNow);
        delivered.RecordDispatch("manual", Actor, DateTime.UtcNow);
        delivered.ConfirmDelivery("manual", Actor, DateTime.UtcNow);
        ((Action)(() => delivered.Cancel(Actor, "superseded", DateTime.UtcNow)))
            .Should().Throw<LetterConflictException>();
    }

    [Fact]
    public void Cancel_validates_the_reason_vocabulary()
    {
        var letter = NewDraft();
        ((Action)(() => letter.Cancel(Actor, "made-up", DateTime.UtcNow)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Content_edits_are_draft_only_and_bump_the_revision()
    {
        var letter = NewDraft();
        letter.AddRecipient(RecipientKind.Person, Guid.NewGuid(), null, null, DateTime.UtcNow);
        var before = letter.Revision;
        letter.UpdateContent("New subject", "New body", DateTime.UtcNow);
        letter.Revision.Should().BeGreaterThan(before);

        letter.Confirm(Actor, DateTime.UtcNow);
        ((Action)(() => letter.UpdateContent("x", "y", DateTime.UtcNow)))
            .Should().Throw<LetterConflictException>();
    }

    [Fact]
    public void Purge_tombstones_carry_the_terminal_status()
    {
        var letter = ConfirmedLetter();
        letter.Submit(Actor, 2026, 7, "default", null, DateTime.UtcNow);
        var tombstone = letter.BuildPurgeTombstone(DateTime.UtcNow);
        tombstone.Cause.Should().Be(HistoryCause.PurgeMarker);
        tombstone.FromStatus.Should().Be(letter.Status);
        tombstone.ToStatus.Should().Be(letter.Status);
    }
}

public sealed class LetterRecipientTests
{
    [Fact]
    public void Recipient_kinds_are_mutually_exclusive_by_identifier()
    {
        var letter = Letter.CreateDraft(Guid.NewGuid(), "general", "S", "B",
            LetterSensitivity.Normal, Guid.NewGuid(), DateTime.UtcNow);

        ((Action)(() => letter.AddRecipient(RecipientKind.Person, null, null, null, DateTime.UtcNow)))
            .Should().Throw<ArgumentException>("person recipients need a person id");
        ((Action)(() => letter.AddRecipient(RecipientKind.Unit, null, null, null, DateTime.UtcNow)))
            .Should().Throw<ArgumentException>("unit recipients need a unit id");
        ((Action)(() => letter.AddRecipient(RecipientKind.External, null, null, null, DateTime.UtcNow)))
            .Should().Throw<ArgumentException>("external recipients need a display line");
        ((Action)(() => letter.AddRecipient(RecipientKind.Person, Guid.NewGuid(), null, "line", DateTime.UtcNow)))
            .Should().Throw<ArgumentException>("display lines are external-only");
    }

    [Fact]
    public void Identical_recipients_conflict_but_distinct_ones_coexist()
    {
        var letter = Letter.CreateDraft(Guid.NewGuid(), "general", "S", "B",
            LetterSensitivity.Normal, Guid.NewGuid(), DateTime.UtcNow);
        var person = Guid.NewGuid();

        letter.AddRecipient(RecipientKind.Person, person, null, null, DateTime.UtcNow);
        ((Action)(() => letter.AddRecipient(RecipientKind.Person, person, null, null, DateTime.UtcNow)))
            .Should().Throw<LetterConflictException>();

        letter.AddRecipient(RecipientKind.Unit, null, Guid.NewGuid(), null, DateTime.UtcNow);
        letter.Recipients.Should().HaveCount(2);
    }

    [Fact]
    public void Recipients_are_frozen_after_confirmation()
    {
        var letter = Letter.CreateDraft(Guid.NewGuid(), "general", "S", "B",
            LetterSensitivity.Normal, Guid.NewGuid(), DateTime.UtcNow);
        letter.AddRecipient(RecipientKind.Person, Guid.NewGuid(), null, null, DateTime.UtcNow);
        letter.Confirm(Guid.NewGuid(), DateTime.UtcNow);

        ((Action)(() => letter.AddRecipient(RecipientKind.Person, Guid.NewGuid(), null, null, DateTime.UtcNow)))
            .Should().Throw<LetterConflictException>();
    }
}

public sealed class OrganizationUnitReferenceTests
{
    [Fact]
    public void Apply_applies_newer_facts_and_rejects_stale_replays()
    {
        var id = Guid.NewGuid();
        var reference = OrganizationUnitReference.Create(id, null, DateTime.UtcNow);
        var parent = Guid.NewGuid();

        reference.Apply(parent, DateTime.UtcNow.AddSeconds(1)).Should().BeTrue();
        reference.Apply(parent, DateTime.UtcNow.AddSeconds(-10)).Should().BeFalse("stale replays are ignored");
    }
}

public sealed class TemplateTests
{
    [Fact]
    public void Template_updates_patch_only_supplied_fields_and_can_reactivate()
    {
        var template = Template.Create("welcome-letter", "Welcome", "Hello", "Body",
            "general", Guid.NewGuid(), DateTime.UtcNow);
        template.IsActive.Should().BeTrue();

        template.Update(null, null, null, null, isActive: false, DateTime.UtcNow);
        template.IsActive.Should().BeFalse();

        template.Update(null, null, null, null, isActive: true, DateTime.UtcNow);
        template.IsActive.Should().BeTrue();
        template.Title.Should().Be("Welcome");
    }
}
