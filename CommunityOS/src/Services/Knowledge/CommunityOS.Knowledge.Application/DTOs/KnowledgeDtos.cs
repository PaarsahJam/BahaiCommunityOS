namespace CommunityOS.Knowledge.Application.DTOs;

// Library

public sealed record WorkDto(
    Guid Id,
    string Title,
    string WorkType,
    string OriginalLanguage,
    string DefaultLanguage,
    IReadOnlyList<EditionDto> Editions);

public sealed record EditionDto(
    Guid Id,
    Guid WorkId,
    string Language,
    string? Translator,
    string? Publisher,
    int? EditionYear,
    bool Verified);

public sealed record PassageDto(
    Guid Id,
    Guid EditionId,
    string ReferencePath,
    string Text,
    int SortOrder,
    int Revision);

// Taxonomy

public sealed record CategoryDto(Guid Id, string Name, string? Description);

public sealed record TopicDto(Guid Id, string Name, string? Description);

// Questions

public sealed record QuestionDto(
    Guid Id,
    string Title,
    string Body,
    string Status,
    Guid AuthorId,
    Guid? CategoryId,
    Guid? OrganizationUnitId,
    Guid? AcceptedAnswerId,
    Guid? MergedOntoQuestionId,
    DateTime CreatedOn,
    IReadOnlyList<string> Tags,
    IReadOnlyList<QuestionLifecycleEventDto> LifecycleEvents);

public sealed record QuestionLifecycleEventDto(
    string FromStatus,
    string ToStatus,
    Guid? ActorId,
    DateTime OccurredOn);

// Answers

public sealed record AnswerDto(
    Guid Id,
    Guid QuestionId,
    Guid AuthorId,
    string Body,
    string Source,
    string? ModelId,
    bool Accepted,
    int Revision);

// Discussions

public sealed record DiscussionDto(
    Guid Id,
    Guid QuestionId,
    string Title,
    string Status,
    Guid AuthorId,
    DateTime CreatedOn,
    IReadOnlyList<CommentDto> Comments);

public sealed record CommentDto(Guid Id, Guid AuthorId, string Body, DateTime CreatedOn);

// AI suggestions

public sealed record AiSuggestionDto(
    Guid Id,
    Guid QuestionId,
    string Body,
    string ModelId,
    string PromptVersion,
    string ReviewState,
    DateTime RequestedOn,
    DateTime? ReviewedOn);

// Read model

public sealed record OrganizationUnitReferenceDto(
    Guid OrganizationUnitId,
    Guid OrganizationId,
    string Name,
    string UnitType,
    Guid? ParentId,
    DateTime LastSeenOn);

public sealed record CitationDto(Guid PassageId, string ReferencePath, string Text, Guid EditionId);