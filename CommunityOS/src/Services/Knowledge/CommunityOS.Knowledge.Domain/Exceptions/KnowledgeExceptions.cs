namespace CommunityOS.Knowledge.Domain.Exceptions;

// Library

public sealed class WorkNotFoundException(Guid workId)
    : Exception($"Work '{workId}' was not found.");

public sealed class EditionNotFoundException(Guid editionId)
    : Exception($"Edition '{editionId}' was not found.");

public sealed class EditionAlreadyVerifiedException(Guid editionId)
    : Exception($"Edition '{editionId}' is already verified.");

public sealed class PassageNotFoundException(Guid passageId)
    : Exception($"Passage '{passageId}' was not found.");

// Questions

public sealed class QuestionNotFoundException(Guid questionId)
    : Exception($"Question '{questionId}' was not found.");

public sealed class InvalidQuestionTransitionException(Guid questionId, string from, string to)
    : Exception($"Question '{questionId}' cannot transition from '{from}' to '{to}'.");

public sealed class QuestionAlreadyTerminalException(Guid questionId, string status)
    : Exception($"Question '{questionId}' is already terminal ('{status}').");

public sealed class InvalidMergeTargetException(Guid questionId, Guid targetId)
    : Exception($"Question '{questionId}' cannot be canonicalized onto '{targetId}': the target is the same question, not found, or terminal.");

// Answers

public sealed class AnswerNotFoundException(Guid answerId)
    : Exception($"Answer '{answerId}' was not found.");

public sealed class AnswerNotOnQuestionException(Guid answerId, Guid questionId)
    : Exception($"Answer '{answerId}' does not belong to question '{questionId}'.");

public sealed class AnswerAlreadyAcceptedException(Guid questionId, Guid answerId)
    : Exception($"Answer '{answerId}' is already the accepted answer of question '{questionId}'.");

public sealed class AlreadyAcceptedAnswerException(Guid questionId)
    : Exception($"Question '{questionId}' already has an accepted answer.");

// References

public sealed class InvalidReferenceException(Guid passageId)
    : Exception($"Reference to passage '{passageId}' is invalid: the passage does not exist or its edition is not verified.");

// Discussions

public sealed class DiscussionNotFoundException(Guid discussionId)
    : Exception($"Discussion '{discussionId}' was not found.");

public sealed class DiscussionAlreadyModeratedException(Guid discussionId)
    : Exception($"Discussion '{discussionId}' is already moderated.");

// AI suggestions

public sealed class AiSuggestionNotFoundException(Guid suggestionId)
    : Exception($"AI suggestion '{suggestionId}' was not found.");

public sealed class AiSuggestionAlreadyReviewedException(Guid suggestionId)
    : Exception($"AI suggestion '{suggestionId}' has already been reviewed.");

public sealed class AiAssistedAnswerRequiresReviewException(Guid suggestionId)
    : Exception($"AI suggestion '{suggestionId}' cannot be made authoritative by the AI itself; acceptance requires a human moderator action.");

// Taxonomy

public sealed class CategoryNotFoundException(Guid categoryId)
    : Exception($"Category '{categoryId}' was not found.");

public sealed class TopicNotFoundException(Guid topicId)
    : Exception($"Topic '{topicId}' was not found.");