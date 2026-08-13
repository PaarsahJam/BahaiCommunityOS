using CommunityOS.Knowledge.Domain.Entities;
using CommunityOS.Knowledge.Domain.Enumerations;
using CommunityOS.Knowledge.Domain.Exceptions;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Knowledge.Domain.Aggregates;

/// <summary>
/// A discussion thread attached to a question. Threads are scoped to the
/// question's organization unit; moderation can hide or delete a thread.
/// Deletion is terminal.
/// </summary>
public sealed class Discussion : AggregateRoot<Guid>
{
    private readonly List<Comment> _comments = [];

    private Discussion() : base(Guid.Empty)
    {
        Title = null!;
        Status = null!;
    }

    private Discussion(
        Guid id,
        Guid questionId,
        Guid? organizationUnitId,
        string title,
        Guid authorId,
        DiscussionStatus status,
        DateTime createdOn) : base(id)
    {
        QuestionId = questionId;
        OrganizationUnitId = organizationUnitId;
        Title = title;
        AuthorId = authorId;
        Status = status;
        CreatedOn = createdOn;
    }

    public Guid QuestionId { get; private set; }
    public Guid? OrganizationUnitId { get; private set; }
    public string Title { get; private set; }
    public Guid AuthorId { get; private set; }
    public DiscussionStatus Status { get; private set; }
    public DateTime CreatedOn { get; private set; }

    public IReadOnlyList<Comment> Comments => _comments.AsReadOnly();

    public static Discussion Create(
        Guid questionId,
        Guid? organizationUnitId,
        string title,
        Guid authorId)
    {
        Guard.NotDefault(questionId, nameof(questionId));
        Guard.NotNullOrWhiteSpace(title, nameof(title));
        Guard.MaxLength(title, 300, nameof(title));
        Guard.NotDefault(authorId, nameof(authorId));

        return new Discussion(
            Guid.NewGuid(),
            questionId,
            organizationUnitId,
            title.Trim(),
            authorId,
            DiscussionStatus.Active,
            DateTime.UtcNow);
    }

    public Comment AddComment(Guid authorId, string body)
    {
        if (Status == DiscussionStatus.Deleted)
            throw new DiscussionAlreadyModeratedException(Id);

        var comment = Comment.Create(authorId, body, DateTime.UtcNow);
        _comments.Add(comment);
        return comment;
    }

    /// <summary>Hides the thread from default views. Active → Hidden.</summary>
    public void Hide()
    {
        if (Status != DiscussionStatus.Active)
            throw new DiscussionAlreadyModeratedException(Id);

        Status = DiscussionStatus.Hidden;
    }

    /// <summary>Deletes the thread (terminal). Active or Hidden → Deleted.</summary>
    public void Delete()
    {
        if (Status == DiscussionStatus.Deleted)
            throw new DiscussionAlreadyModeratedException(Id);

        Status = DiscussionStatus.Deleted;
    }
}