using CommunityOS.Knowledge.Domain.Entities;
using CommunityOS.Knowledge.Domain.Enumerations;
using CommunityOS.Knowledge.Domain.Events;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Knowledge.Domain.Aggregates;

/// <summary>
/// A community answer to a question. Edits append revision records; the body is
/// never mutated in place. Answers sourced from AI (source = "ai") are always
/// created as the result of an accepted AiSuggestion and remain traceable via
/// ModelId and SuggestionId.
/// </summary>
public sealed class Answer : AggregateRoot<Guid>
{
    private readonly List<AnswerRevision> _revisions = [];

    private Answer() : base(Guid.Empty)
    {
        Body = null!;
        Source = null!;
    }

    private Answer(
        Guid id,
        Guid questionId,
        Guid authorId,
        string body,
        AnswerSource source,
        string? modelId,
        Guid? suggestionId,
        bool accepted) : base(id)
    {
        QuestionId = questionId;
        AuthorId = authorId;
        Body = body;
        Source = source;
        ModelId = modelId;
        SuggestionId = suggestionId;
        Accepted = accepted;
        _revisions.Add(AnswerRevision.Initial(body));
    }

    public Guid QuestionId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Body { get; private set; }
    public AnswerSource Source { get; private set; }
    public string? ModelId { get; private set; }
    public Guid? SuggestionId { get; private set; }
    public bool Accepted { get; private set; }
    public int Revision => _revisions.Count;

    public IReadOnlyList<AnswerRevision> Revisions => _revisions.AsReadOnly();

    public static Answer Create(Guid questionId, Guid authorId, string body)
    {
        Guard.NotDefault(questionId, nameof(questionId));
        Guard.NotDefault(authorId, nameof(authorId));
        Guard.NotNullOrWhiteSpace(body, nameof(body));
        Guard.MaxLength(body, 20000, nameof(body));

        var answer = new Answer(
            Guid.NewGuid(),
            questionId,
            authorId,
            body.Trim(),
            AnswerSource.Member,
            modelId: null,
            suggestionId: null,
            accepted: false);

        answer.RaiseDomainEvent(new AnswerAddedEvent(answer.Id, questionId, authorId, answer.Source.Name));
        return answer;
    }

    /// <summary>
    /// Creates an answer as the result of an accepted AI suggestion. Human
    /// review (the suggestion's acceptance) is the only path to an AI answer.
    /// </summary>
    public static Answer CreateFromAiSuggestion(
        Guid questionId,
        Guid reviewerId,
        string body,
        string modelId,
        Guid suggestionId)
    {
        Guard.NotDefault(questionId, nameof(questionId));
        Guard.NotDefault(reviewerId, nameof(reviewerId));
        Guard.NotNullOrWhiteSpace(body, nameof(body));
        Guard.MaxLength(body, 20000, nameof(body));
        Guard.NotNullOrWhiteSpace(modelId, nameof(modelId));
        Guard.MaxLength(modelId, 200, nameof(modelId));
        Guard.NotDefault(suggestionId, nameof(suggestionId));

        var answer = new Answer(
            Guid.NewGuid(),
            questionId,
            reviewerId,
            body.Trim(),
            AnswerSource.Ai,
            modelId.Trim(),
            suggestionId,
            accepted: false);

        answer.RaiseDomainEvent(new AnswerAddedEvent(answer.Id, questionId, reviewerId, answer.Source.Name));
        return answer;
    }

    /// <summary>
    /// Appends a new revision to the answer. The body is immutable; the current
    /// text is always the latest revision.
    /// </summary>
    public void Update(string body)
    {
        Guard.NotNullOrWhiteSpace(body, nameof(body));
        Guard.MaxLength(body, 20000, nameof(body));

        Body = body.Trim();
        _revisions.Add(AnswerRevision.Of(_revisions.Count + 1, body.Trim()));
        RaiseDomainEvent(new AnswerUpdatedEvent(Id, QuestionId, Revision));
    }

    public void MarkAccepted() => Accepted = true;
}