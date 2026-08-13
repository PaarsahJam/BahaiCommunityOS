using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Knowledge.Domain.Events;

// Library

public sealed record WorkImportedEvent(Guid WorkId, string Title, string WorkType) : DomainEvent;

public sealed record WorkUpdatedEvent(Guid WorkId, string Title, string WorkType) : DomainEvent;

public sealed record EditionImportedEvent(Guid EditionId, Guid WorkId, string Language, bool Verified) : DomainEvent;

public sealed record EditionVerifiedEvent(Guid EditionId, Guid WorkId) : DomainEvent;

public sealed record PassageImportedEvent(Guid PassageId, Guid EditionId, string ReferencePath) : DomainEvent;

public sealed record PassageCorrectedEvent(Guid PassageId, Guid EditionId, int Revision) : DomainEvent;

// Questions

public sealed record QuestionSubmittedEvent(Guid QuestionId, Guid AuthorId, Guid? OrganizationUnitId) : DomainEvent;

public sealed record QuestionPublishedEvent(Guid QuestionId, Guid? OrganizationUnitId) : DomainEvent;

public sealed record QuestionFlaggedEvent(Guid QuestionId, string FlagReason) : DomainEvent;

public sealed record QuestionUnderReviewEvent(Guid QuestionId) : DomainEvent;

public sealed record QuestionMergedEvent(Guid QuestionId, Guid TargetQuestionId) : DomainEvent;

public sealed record QuestionArchivedEvent(Guid QuestionId) : DomainEvent;

// Answers

public sealed record AnswerAddedEvent(Guid AnswerId, Guid QuestionId, Guid AuthorId, string Source) : DomainEvent;

public sealed record AnswerUpdatedEvent(Guid AnswerId, Guid QuestionId, int Revision) : DomainEvent;

public sealed record AnswerAcceptedEvent(Guid AnswerId, Guid QuestionId) : DomainEvent;

// AI suggestions

public sealed record AiSuggestionRequestedEvent(Guid SuggestionId, Guid QuestionId, string ModelId) : DomainEvent;

public sealed record AiSuggestionReviewedEvent(Guid SuggestionId, Guid QuestionId, string Outcome) : DomainEvent;

// Taxonomy

public sealed record CategoryCreatedEvent(Guid CategoryId, string Name) : DomainEvent;

public sealed record CategoryUpdatedEvent(Guid CategoryId, string Name) : DomainEvent;

public sealed record TopicCreatedEvent(Guid TopicId, string Name) : DomainEvent;

public sealed record TopicUpdatedEvent(Guid TopicId, string Name) : DomainEvent;