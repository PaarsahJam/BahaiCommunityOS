namespace CommunityOS.Workflow.Domain.Enumerations;

/// <summary>
/// Canonical string values for a workflow task definition code. Codes are stable
/// strings (never a hard enum) seeded idempotently at migration time and
/// extensible at runtime via the definitions catalog.
/// </summary>
public static class WorkflowDefinitionCodes
{
    public const string RecordReview = "record-review";
    public const string DocumentReview = "document-review";
    public const string KnowledgeModeration = "knowledge-moderation";
    public const string KnowledgeAiReview = "knowledge-ai-review";
    public const string General = "general";

    public static IReadOnlyList<string> Baseline =>
        [RecordReview, DocumentReview, KnowledgeModeration, KnowledgeAiReview, General];
}

/// <summary>
/// Canonical string values for the <c>DomainType</c> a task works over.
/// </summary>
public static class WorkflowDomainTypes
{
    public const string Record = "record";
    public const string Document = "document";
    public const string KnowledgeQuestion = "knowledge-question";
    public const string KnowledgeAiSuggestion = "knowledge-ai-suggestion";
    public const string General = "general";

    public static bool IsValid(string? domainType) =>
        domainType is not null &&
        (string.Equals(domainType, Record, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(domainType, Document, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(domainType, KnowledgeQuestion, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(domainType, KnowledgeAiSuggestion, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(domainType, General, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Canonical string values for a definition-permitted outcome code. Reject is an
/// outcome, never a lifecycle state.
/// </summary>
public static class WorkflowOutcomes
{
    public const string Verified = "verified";
    public const string Rejected = "rejected";
    public const string Approved = "approved";
    public const string ClarificationRequested = "clarification-requested";
    public const string Merged = "merged";
    public const string Archived = "archived";
    public const string Accepted = "accepted";

    public static bool IsValid(string? outcome) =>
        outcome is not null &&
        (string.Equals(outcome, Verified, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(outcome, Rejected, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(outcome, Approved, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(outcome, ClarificationRequested, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(outcome, Merged, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(outcome, Archived, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(outcome, Accepted, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Canonical string values for a task activity action (append-only history).
/// Notes are sensitive and never exported.
/// </summary>
public static class WorkflowActivityActions
{
    public const string Created = "created";
    public const string Assigned = "assigned";
    public const string Started = "started";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string Escalated = "escalated";
    public const string Note = "note";
}

/// <summary>
/// Ratified baseline <c>dueIn</c> ISO-8601 duration used when a definition
/// provides no explicit SLA policy.
/// </summary>
public static class WorkflowDefaults
{
    public const string DueIn = "P14D";
}