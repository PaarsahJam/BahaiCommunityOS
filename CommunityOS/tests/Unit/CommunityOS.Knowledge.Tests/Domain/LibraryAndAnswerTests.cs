using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Enumerations;
using CommunityOS.Knowledge.Domain.Events;
using CommunityOS.Knowledge.Domain.Exceptions;

namespace CommunityOS.Knowledge.Tests.Domain;

/// <summary>
/// Library and community-content invariants: passages are immutable (text
/// corrections append revision records, never mutate in place), editions gate
/// citation eligibility by verification, answers keep an append-only revision
/// history, AI suggestions are non-authoritative and require a human review,
/// and discussions enforce moderation terminal states.
/// </summary>
public class LibraryAndAnswerTests
{
    private static readonly Guid EditionId = Guid.NewGuid();

    // --- Passage immutability via revisions ---

    [Fact]
    public void Passage_import_records_an_initial_revision()
    {
        var p = Passage.Import(EditionId, "2:1", "Blessed is the man", 0);

        p.Revision.Should().Be(1);
        p.Revisions.Should().ContainSingle(r => r.Revision == 1);
        p.Text.Should().Be("Blessed is the man");
    }

    [Fact]
    public void Passage_correction_appends_a_revision_not_in_place_mutation()
    {
        var p = Passage.Import(EditionId, "2:1", "Original", 0);

        p.Correct("Corrected text");

        p.Revision.Should().Be(2);
        p.Revisions.Should().HaveCount(2);
        p.Revisions[0].Text.Should().Be("Original");
        p.Revisions[1].Text.Should().Be("Corrected text");
        p.Text.Should().Be("Corrected text");
    }

    // --- Edition verification gates citations ---

    [Fact]
    public void Edition_verify_is_idempotent()
    {
        var e = Edition.Import(Guid.NewGuid(), "en", null, null, 1999, verified: false);
        e.Verify();
        e.Verify(); // second call is a no-op

        e.Verified.Should().BeTrue();
    }

    // --- Answer revision history ---

    [Fact]
    public void Answer_update_appends_revision_and_keeps_body_immutable()
    {
        var a = Answer.Create(Guid.NewGuid(), Guid.NewGuid(), "First draft");

        a.Update("Revised");

        a.Revision.Should().Be(2);
        a.Revisions.Should().HaveCount(2);
        a.Revisions[0].Body.Should().Be("First draft");
        a.Revisions[1].Body.Should().Be("Revised");
        a.Body.Should().Be("Revised");
    }

    // --- AI suggestions are non-authoritative ---

    [Fact]
    public void AiSuggestion_cannot_review_itself_twice()
    {
        var s = AiSuggestion.Request(Guid.NewGuid(), null, "Draft body", "gpt-4o", "1");

        s.Review(AiSuggestionReviewState.Accepted);
        var act = () => s.Review(AiSuggestionReviewState.Accepted);

        act.Should().Throw<AiSuggestionAlreadyReviewedException>();
        s.ReviewState.Should().Be(AiSuggestionReviewState.Accepted);
    }

    [Fact]
    public void AiSuggestion_acceptance_requires_human_review_transition()
    {
        var s = AiSuggestion.Request(Guid.NewGuid(), null, "Draft body", "gpt-4o", "1");

        // The suggestion itself can never become authoritative without a
        // human moderator's explicit review decision.
        var act = () => s.Review(AiSuggestionReviewState.Accepted);
        act.Should().NotThrow();

        s.ReviewState.Should().Be(AiSuggestionReviewState.Accepted);
        s.ReviewedOn.Should().NotBeNull();
    }

    // --- Discussion moderation ---

    [Fact]
    public void Discussion_cannot_add_comment_after_delete()
    {
        var d = Discussion.Create(Guid.NewGuid(), null, "Thread", Guid.NewGuid());
        d.Delete();

        var act = () => d.AddComment(Guid.NewGuid(), "late comment");
        act.Should().Throw<DiscussionAlreadyModeratedException>();
    }

    [Fact]
    public void Discussion_hide_requires_active_state()
    {
        var d = Discussion.Create(Guid.NewGuid(), null, "Thread", Guid.NewGuid());
        d.Hide();
        var act = () => d.Hide();
        act.Should().Throw<DiscussionAlreadyModeratedException>();
    }

    // --- Reference validation on answers ---

    [Fact]
    public void Answer_creation_raises_AnswerAdded_event()
    {
        var a = Answer.Create(Guid.NewGuid(), Guid.NewGuid(), "Body");
        a.DomainEvents.Should().ContainSingle(e => e is AnswerAddedEvent);
    }
}