namespace CommunityOS.Knowledge.Application.Permissions;

/// <summary>
/// Central registry of well-known Knowledge permission names
/// (<c>knowledge.entity.action</c>). Enforced by the Knowledge application
/// layer through the Authorization guard. Library operations use global
/// contexts (work/edition/passage); question, answer, AI, and discussion
/// operations are org-scoped by the question's organization unit.
/// </summary>
public static class KnowledgePermissions
{
    // Library
    public const string LibraryRead = "knowledge.library.read";
    public const string LibraryImport = "knowledge.library.import";
    public const string LibraryUpdate = "knowledge.library.update";
    public const string LibraryVerify = "knowledge.library.verify";

    // Questions
    public const string QuestionRead = "knowledge.question.read";
    public const string QuestionCreate = "knowledge.question.create";
    public const string QuestionUpdate = "knowledge.question.update";
    public const string QuestionMerge = "knowledge.question.merge";

    // Answers
    public const string AnswerRead = "knowledge.answer.read";
    public const string AnswerCreate = "knowledge.answer.create";
    public const string AnswerUpdate = "knowledge.answer.update";

    // Moderation
    public const string ModerationReview = "knowledge.moderation.review";
    public const string ModerationArchive = "knowledge.moderation.archive";
    public const string ModerationManage = "knowledge.moderation.manage";

    // AI
    public const string AiReview = "knowledge.ai.review";
    public const string AiSuggest = "knowledge.ai.suggest";

    // Discussions
    public const string DiscussionRead = "knowledge.discussion.read";
    public const string DiscussionCreate = "knowledge.discussion.create";
    public const string DiscussionModerate = "knowledge.discussion.moderate";
}