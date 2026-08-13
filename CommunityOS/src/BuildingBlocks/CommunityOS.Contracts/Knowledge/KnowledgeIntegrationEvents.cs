namespace CommunityOS.Contracts.Knowledge;

/// <summary>
/// Raised when a Library work is imported. The work carries a stable id,
/// title and type; editions are imported and verified separately.
/// </summary>
public sealed record WorkImported(Guid WorkId, string Title, string WorkType, DateTime OccurredOn);

/// <summary>
/// Raised when an edition of a work is imported. Carries the verification
/// state so consumers can track which editions are citable.
/// </summary>
public sealed record EditionImported(Guid EditionId, Guid WorkId, string Language, bool Verified, DateTime OccurredOn);

/// <summary>
/// Raised when an edition becomes verified (citable by community content).
/// </summary>
public sealed record EditionVerified(Guid EditionId, Guid WorkId, DateTime OccurredOn);

/// <summary>
/// Raised when a passage is imported into an edition. Carries the
/// original-language reference path; the authoritative text is always resolved
/// through the Knowledge Library API.
/// </summary>
public sealed record PassageImported(Guid PassageId, Guid EditionId, string ReferencePath, DateTime OccurredOn);

/// <summary>
/// Raised when a passage text correction is published. Passages are immutable;
/// a correction produces a new revision, never an in-place mutation.
/// </summary>
public sealed record PassageCorrected(Guid PassageId, Guid EditionId, int Revision, DateTime OccurredOn);

/// <summary>
/// Raised when a question transitions Draft → Submitted. Carries the author
/// (a stable person id) and the scoping organization unit. No PII is exported.
/// </summary>
public sealed record QuestionSubmitted(
    Guid QuestionId,
    Guid AuthorId,
    Guid? OrganizationUnitId,
    DateTime OccurredOn);

/// <summary>
/// Raised when a question is published (Submitted → Published) by a moderator.
/// </summary>
public sealed record QuestionPublished(Guid QuestionId, Guid? OrganizationUnitId, DateTime OccurredOn);

/// <summary>
/// Raised when a question is flagged for review. Carries the flag reason only;
/// the flagger is resolved through the Knowledge API.
/// </summary>
public sealed record QuestionFlagged(Guid QuestionId, string FlagReason, DateTime OccurredOn);

/// <summary>
/// Raised when a published question is moved under review.
/// </summary>
public sealed record QuestionUnderReview(Guid QuestionId, DateTime OccurredOn);

/// <summary>
/// Raised when a question is canonicalized onto a target (terminal). The
/// target's canonicalization is immutable once applied.
/// </summary>
public sealed record QuestionMerged(Guid QuestionId, Guid TargetQuestionId, DateTime OccurredOn);

/// <summary>
/// Raised when a question is archived (terminal). History is preserved.
/// </summary>
public sealed record QuestionArchived(Guid QuestionId, DateTime OccurredOn);

/// <summary>
/// Raised when an answer is added. Carries the author (a stable person id) and
/// source ("member" or "ai"). No author PII is exported.
/// </summary>
public sealed record AnswerAdded(Guid AnswerId, Guid QuestionId, Guid AuthorId, string Source, DateTime OccurredOn);

/// <summary>
/// Raised when an answer gets a new revision. Carries the current revision
/// number; revision history is read through the Knowledge API.
/// </summary>
public sealed record AnswerUpdated(Guid AnswerId, Guid QuestionId, int Revision, DateTime OccurredOn);

/// <summary>
/// Raised when an answer is marked accepted for its question (one per question).
/// </summary>
public sealed record AnswerAccepted(Guid AnswerId, Guid QuestionId, DateTime OccurredOn);

/// <summary>
/// Raised when an AI-generated draft is requested for a question. The draft is
/// a non-authoritative suggestion; it is never an accepted answer until a human
/// moderator accepts it.
/// </summary>
public sealed record AiSuggestionRequested(Guid SuggestionId, Guid QuestionId, string ModelId, DateTime OccurredOn);

/// <summary>
/// Raised when an AI suggestion is accepted or rejected by a human moderator.
/// </summary>
public sealed record AiSuggestionReviewed(Guid SuggestionId, Guid QuestionId, string Outcome, DateTime OccurredOn);

/// <summary>
/// Raised when a knowledge category is created.
/// </summary>
public sealed record CategoryCreated(Guid CategoryId, string Name, DateTime OccurredOn);

/// <summary>
/// Raised when a knowledge category is updated.
/// </summary>
public sealed record CategoryUpdated(Guid CategoryId, string Name, DateTime OccurredOn);