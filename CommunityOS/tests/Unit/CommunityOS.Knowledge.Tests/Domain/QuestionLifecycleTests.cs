using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Enumerations;
using CommunityOS.Knowledge.Domain.Exceptions;

namespace CommunityOS.Knowledge.Tests.Domain;

/// <summary>
/// Ratified question lifecycle invariants:
/// <c>Draft → Submitted → Published → Under Review → Merged | Archived</c>.
/// Merged/Archived are terminal, canonicalization is immutable, and only one
/// accepted answer is allowed per question.
/// </summary>
public class QuestionLifecycleTests
{
    private static Question NewQuestion() => Question.Create(
        "How do I study the Kitab-i-Aqdas?",
        "What is a recommended approach for studying the Most Holy Book?",
        Guid.NewGuid(),
        categoryId: null,
        organizationUnitId: Guid.NewGuid());

    // --- Happy path ---

    [Fact]
    public void Question_starts_as_draft()
    {
        var q = NewQuestion();
        q.Status.Should().Be(QuestionStatus.Draft);
        q.AcceptedAnswerId.Should().BeNull();
        q.MergedOntoQuestionId.Should().BeNull();
        q.Tags.Should().BeEmpty();
        q.LifecycleEvents.Should().ContainSingle(e => e.FromStatus == "none" && e.ToStatus == QuestionStatus.Draft.Name);
    }

    [Fact]
    public void Question_can_be_published_after_submission()
    {
        var q = NewQuestion();
        q.Submit();
        q.Publish();
        q.MoveUnderReview();

        q.Status.Should().Be(QuestionStatus.UnderReview);
        q.LifecycleEvents.Should().Contain(e => e.FromStatus == QuestionStatus.Draft.Name && e.ToStatus == QuestionStatus.Submitted.Name);
        q.LifecycleEvents.Should().Contain(e => e.FromStatus == QuestionStatus.Submitted.Name && e.ToStatus == QuestionStatus.Published.Name);
        q.LifecycleEvents.Should().Contain(e => e.FromStatus == QuestionStatus.Published.Name && e.ToStatus == QuestionStatus.UnderReview.Name);
    }

    // --- Transition guards ---

    [Fact]
    public void Publish_without_submission_is_rejected()
    {
        var q = NewQuestion();
        var act = () => q.Publish();
        act.Should().Throw<InvalidQuestionTransitionException>()
            .WithMessage($"*{QuestionStatus.Draft.Name}*{QuestionStatus.Published.Name}*");
    }

    [Fact]
    public void Submit_twice_is_rejected()
    {
        var q = NewQuestion();
        q.Submit();
        var act = () => q.Submit();
        act.Should().Throw<InvalidQuestionTransitionException>();
    }

    [Fact]
    public void MoveUnderReview_from_draft_is_rejected()
    {
        var q = NewQuestion();
        var act = () => q.MoveUnderReview();
        act.Should().Throw<InvalidQuestionTransitionException>();
    }

    // --- Flagging is allowed in any non-terminal state ---

    [Fact]
    public void Flag_appends_a_moderation_flag()
    {
        var q = NewQuestion();
        q.Flag("Duplicate question");
        q.Flag("Requires citation");

        q.ModerationFlags.Should().HaveCount(2);
        q.ModerationFlags[0].Reason.Should().Be("Duplicate question");
        q.ModerationFlags[1].Reason.Should().Be("Requires citation");
    }

    // --- Merge / canonicalize ---

    [Fact]
    public void MergeOnto_canonicalizes_and_is_immutable()
    {
        var q = NewQuestion();
        var target = Question.Create("Original question", "Original body", Guid.NewGuid(), null, null);
        var targetId = target.Id;

        q.MergeOnto(targetId);
        target.Canonicalize();

        q.Status.Should().Be(QuestionStatus.Merged);
        q.MergedOntoQuestionId.Should().Be(targetId);
        target.Status.Should().Be(QuestionStatus.Canonicalized);

        var act = () => q.MergeOnto(Guid.NewGuid());
        act.Should().Throw<QuestionAlreadyTerminalException>();
    }

    [Fact]
    public void MergeOnto_itself_is_rejected()
    {
        var q = NewQuestion();
        var act = () => q.MergeOnto(q.Id);
        act.Should().Throw<InvalidMergeTargetException>();
    }

    [Fact]
    public void MergeOnto_when_terminal_is_rejected()
    {
        var q = NewQuestion();
        q.Archive();
        var act = () => q.MergeOnto(Guid.NewGuid());
        act.Should().Throw<QuestionAlreadyTerminalException>();
    }

    // --- Terminal states ---

    [Fact]
    public void Archive_is_terminal_and_history_is_preserved()
    {
        var q = NewQuestion();
        q.Submit();
        q.Archive();

        q.Status.Should().Be(QuestionStatus.Archived);
        q.LifecycleEvents.Should().HaveCount(3);

        var act = () => q.Archive();
        act.Should().Throw<QuestionAlreadyTerminalException>();
    }

    // --- Accepted answer ---

    [Fact]
    public void Only_one_accepted_answer_is_allowed()
    {
        var q = NewQuestion();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        q.AcceptAnswer(first);
        q.AcceptedAnswerId.Should().Be(first);

        var act = () => q.AcceptAnswer(second);
        act.Should().Throw<AlreadyAcceptedAnswerException>();
    }

    // --- Tags ---

    [Fact]
    public void Tags_are_trimmed_on_create()
    {
        var q = Question.Create(
            "Title", "Body", Guid.NewGuid(), null, null,
            tags: [" Study ", "Prayer", "study "]);

        q.Tags.Should().HaveCount(3);
        q.Tags[0].Name.Should().Be("Study");
        q.Tags[2].Name.Should().Be("study");
    }
}