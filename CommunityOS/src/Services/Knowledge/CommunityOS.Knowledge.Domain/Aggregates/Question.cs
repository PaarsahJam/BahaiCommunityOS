using CommunityOS.Knowledge.Domain.Entities;
using CommunityOS.Knowledge.Domain.Enumerations;
using CommunityOS.Knowledge.Domain.Events;
using CommunityOS.Knowledge.Domain.Exceptions;
using CommunityOS.SharedKernel.Domain.Events;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Knowledge.Domain.Aggregates;

/// <summary>
/// A community question. Follows the lifecycle
/// <c>Draft → Submitted → Published → Under Review → Merged | Archived</c>.
/// A <c>Canonicalized</c> question is the surviving target of one or more
/// merges; <c>Merged</c> and <c>Archived</c> are terminal. Canonicalization is
/// immutable once applied. The question is scoped to an organization unit
/// (a reference, never owned) so moderation is jurisdiction-aware.
/// </summary>
public sealed class Question : AggregateRoot<Guid>
{
    private readonly List<QuestionTag> _tags = [];
    private readonly List<ModerationFlag> _moderationFlags = [];
    private readonly List<QuestionLifecycleEvent> _lifecycleEvents = [];

    private Question() : base(Guid.Empty)
    {
        Title = null!;
        Body = null!;
        Status = null!;
    }

    private Question(
        Guid id,
        string title,
        string body,
        Guid authorId,
        Guid? categoryId,
        Guid? organizationUnitId,
        IEnumerable<string> tags,
        DateTime createdOn) : base(id)
    {
        Title = title;
        Body = body;
        AuthorId = authorId;
        CategoryId = categoryId;
        OrganizationUnitId = organizationUnitId;
        Status = QuestionStatus.Draft;
        CreatedOn = createdOn;
        _tags.AddRange(tags.Select(QuestionTag.Create));
        _lifecycleEvents.Add(QuestionLifecycleEvent.Create("none", Status.Name, authorId, createdOn));
    }

    public string Title { get; private set; }
    public string Body { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Guid? OrganizationUnitId { get; private set; }
    public QuestionStatus Status { get; private set; }
    public Guid? AcceptedAnswerId { get; private set; }
    public Guid? MergedOntoQuestionId { get; private set; }
    public DateTime CreatedOn { get; private set; }

    public IReadOnlyList<QuestionTag> Tags => _tags.AsReadOnly();
    public IReadOnlyList<ModerationFlag> ModerationFlags => _moderationFlags.AsReadOnly();
    public IReadOnlyList<QuestionLifecycleEvent> LifecycleEvents => _lifecycleEvents.AsReadOnly();

    public static Question Create(
        string title,
        string body,
        Guid authorId,
        Guid? categoryId,
        Guid? organizationUnitId,
        IEnumerable<string>? tags = null)
    {
        Guard.NotNullOrWhiteSpace(title, nameof(title));
        Guard.MaxLength(title, 300, nameof(title));
        Guard.NotNullOrWhiteSpace(body, nameof(body));
        Guard.MaxLength(body, 10000, nameof(body));
        Guard.NotDefault(authorId, nameof(authorId));

        return new Question(
            Guid.NewGuid(),
            title.Trim(),
            body.Trim(),
            authorId,
            categoryId,
            organizationUnitId,
            tags ?? [],
            DateTime.UtcNow);
    }

    /// <summary>Draft → Submitted.</summary>
    public void Submit()
    {
        RequireTransition(QuestionStatus.Submitted, from: QuestionStatus.Draft);
        MoveTo(QuestionStatus.Submitted, raise: new QuestionSubmittedEvent(Id, AuthorId, OrganizationUnitId));
    }

    /// <summary>Submitted → Published. Requires a moderator grant.</summary>
    public void Publish()
    {
        RequireTransition(QuestionStatus.Published, from: QuestionStatus.Submitted);
        MoveTo(QuestionStatus.Published, raise: new QuestionPublishedEvent(Id, OrganizationUnitId));
    }

    /// <summary>Flags the question for review. Allowed in any non-terminal state.</summary>
    public void Flag(string reason)
    {
        Guard.NotNullOrWhiteSpace(reason, nameof(reason));
        Guard.MaxLength(reason, 500, nameof(reason));

        _moderationFlags.Add(ModerationFlag.Create(reason.Trim(), null, OccurredAt()));
        RaiseDomainEvent(new QuestionFlaggedEvent(Id, reason.Trim()));
    }

    /// <summary>Published → Under Review. Requires a moderator grant.</summary>
    public void MoveUnderReview()
    {
        RequireTransition(QuestionStatus.UnderReview, from: QuestionStatus.Published);
        MoveTo(QuestionStatus.UnderReview, raise: new QuestionUnderReviewEvent(Id));
    }

    /// <summary>
    /// Canonicalizes this question onto a target (terminal). The target becomes
    /// canonicalized; this question's canonical target is immutable once set.
    /// </summary>
    public void MergeOnto(Guid targetQuestionId)
    {
        Guard.NotDefault(targetQuestionId, nameof(targetQuestionId));

        if (Status == QuestionStatus.Merged || Status == QuestionStatus.Archived)
            throw new QuestionAlreadyTerminalException(Id, Status.Name);
        if (targetQuestionId == Id)
            throw new InvalidMergeTargetException(Id, targetQuestionId);
        if (MergedOntoQuestionId is not null)
            throw new InvalidQuestionTransitionException(Id, Status.Name, QuestionStatus.Merged.Name);

        MoveTo(
            QuestionStatus.Merged,
            raise: new QuestionMergedEvent(Id, targetQuestionId));
        MergedOntoQuestionId = targetQuestionId;
    }

    /// <summary>
    /// Marks this question as the surviving canonical target of a merge.
    /// Canonicalization is immutable once applied.
    /// </summary>
    public void Canonicalize()
    {
        if (Status == QuestionStatus.Merged || Status == QuestionStatus.Archived)
            throw new QuestionAlreadyTerminalException(Id, Status.Name);

        MoveTo(QuestionStatus.Canonicalized, raise: null);
    }

    /// <summary>Archives this question (terminal). History is preserved.</summary>
    public void Archive()
    {
        if (Status == QuestionStatus.Merged || Status == QuestionStatus.Archived)
            throw new QuestionAlreadyTerminalException(Id, Status.Name);

        MoveTo(QuestionStatus.Archived, raise: new QuestionArchivedEvent(Id));
    }

    /// <summary>
    /// Marks an answer as the accepted answer. Only one accepted answer is
    /// allowed per question; accepting a second one is rejected.
    /// </summary>
    public void AcceptAnswer(Guid answerId)
    {
        Guard.NotDefault(answerId, nameof(answerId));

        if (AcceptedAnswerId is not null)
            throw new AlreadyAcceptedAnswerException(Id);

        AcceptedAnswerId = answerId;
        RaiseDomainEvent(new AnswerAcceptedEvent(answerId, Id));
    }

    private void RequireTransition(QuestionStatus to, QuestionStatus from)
    {
        if (Status != from)
            throw new InvalidQuestionTransitionException(Id, Status.Name, to.Name);
    }

    private void MoveTo(QuestionStatus to, IDomainEvent? raise)
    {
        var from = Status.Name;
        Status = to;
        _lifecycleEvents.Add(QuestionLifecycleEvent.Create(from, to.Name, null, OccurredAt()));
        if (raise is not null) RaiseDomainEvent(raise);
    }

    private static DateTime OccurredAt() => DateTime.UtcNow;
}