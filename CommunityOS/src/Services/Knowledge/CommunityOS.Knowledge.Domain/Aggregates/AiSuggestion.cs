using CommunityOS.Knowledge.Domain.Enumerations;
using CommunityOS.Knowledge.Domain.Events;
using CommunityOS.Knowledge.Domain.Exceptions;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Knowledge.Domain.Aggregates;

/// <summary>
/// An AI-generated answer suggestion for a question. Suggestions are never
/// authoritative: only a human moderator may accept or reject them. Acceptance
/// transitions the suggestion to Accepted and is the sole path to creating an
/// AI-sourced answer.
/// </summary>
public sealed class AiSuggestion : AggregateRoot<Guid>
{
    private AiSuggestion() : base(Guid.Empty)
    {
        Body = null!;
        ModelId = null!;
        PromptVersion = null!;
        ReviewState = null!;
    }

    private AiSuggestion(
        Guid id,
        Guid questionId,
        Guid? organizationUnitId,
        string body,
        string modelId,
        string promptVersion,
        DateTime requestedOn) : base(id)
    {
        QuestionId = questionId;
        OrganizationUnitId = organizationUnitId;
        Body = body;
        ModelId = modelId;
        PromptVersion = promptVersion;
        RequestedOn = requestedOn;
        ReviewState = AiSuggestionReviewState.Suggested;
    }

    public Guid QuestionId { get; private set; }
    public Guid? OrganizationUnitId { get; private set; }
    public string Body { get; private set; }
    public string ModelId { get; private set; }
    public string PromptVersion { get; private set; }
    public DateTime RequestedOn { get; private set; }
    public AiSuggestionReviewState ReviewState { get; private set; }
    public DateTime? ReviewedOn { get; private set; }

    public static AiSuggestion Request(
        Guid questionId,
        Guid? organizationUnitId,
        string body,
        string modelId,
        string promptVersion)
    {
        Guard.NotDefault(questionId, nameof(questionId));
        Guard.NotNullOrWhiteSpace(body, nameof(body));
        Guard.MaxLength(body, 20000, nameof(body));
        Guard.NotNullOrWhiteSpace(modelId, nameof(modelId));
        Guard.MaxLength(modelId, 200, nameof(modelId));
        Guard.NotNullOrWhiteSpace(promptVersion, nameof(promptVersion));
        Guard.MaxLength(promptVersion, 100, nameof(promptVersion));

        var suggestion = new AiSuggestion(
            Guid.NewGuid(),
            questionId,
            organizationUnitId,
            body.Trim(),
            modelId.Trim(),
            promptVersion.Trim(),
            DateTime.UtcNow);

        suggestion.RaiseDomainEvent(new AiSuggestionRequestedEvent(
            suggestion.Id, questionId, suggestion.ModelId));
        return suggestion;
    }

    /// <summary>
    /// Human review outcome. Acceptance is the only authorized transition; a
    /// suggestion can never accept itself. Both transitions are terminal.
    /// </summary>
    public void Review(AiSuggestionReviewState outcome)
    {
        Guard.NotNull(outcome, nameof(outcome));

        if (ReviewState != AiSuggestionReviewState.Suggested)
            throw new AiSuggestionAlreadyReviewedException(Id);

        ReviewState = outcome;
        ReviewedOn = DateTime.UtcNow;

        RaiseDomainEvent(new AiSuggestionReviewedEvent(Id, QuestionId, outcome.Name));
    }
}