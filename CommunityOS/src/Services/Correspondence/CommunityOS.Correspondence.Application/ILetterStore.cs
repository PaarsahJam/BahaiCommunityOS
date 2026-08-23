using CommunityOS.Correspondence.Domain;

namespace CommunityOS.Correspondence.Application;

/// <summary>
/// Read side: deterministic candidate queries and lookups. Implementations
/// apply filters, projections and ordering at the store; visibility is decided
/// by the handlers through the Authorization service (ADR-028 decision 15).
/// </summary>
public interface ILetterReader
{
    /// <summary>Returns up to <paramref name="maxRows"/> metadata-only
    /// summaries matching the filters, ordered by submission/creation time in
    /// the requested direction (then id) — the deterministic order handlers
    /// walk when authorizing.</summary>
    Task<IReadOnlyList<LetterSummaryRow>> QueryAsync(
        LetterQueryFilters filters, bool ascending, int offset, int maxRows, CancellationToken ct);

    Task<LetterSummaryRow?> FindSummaryAsync(Guid id, CancellationToken ct);

    Task<LetterDetailRow?> FindDetailAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<LetterSummaryRow>> FindRangeSummariesAsync(IReadOnlyList<Guid> ids, CancellationToken ct);

    Task<IReadOnlyList<HistoryRow>> GetHistoryAsync(Guid letterId, CancellationToken ct);

    Task<bool> HasActiveHoldAsync(Guid letterId, CancellationToken ct);

    Task<LetterHold?> FindHoldAsync(Guid holdId, CancellationToken ct);

    Task<int> CountExpiredUnheldAsync(DateTime asOf, CancellationToken ct);

    Task<IReadOnlyList<TemplateRow>> ListActiveTemplatesAsync(CancellationToken ct);

    Task<Template?> FindTemplateAsync(Guid id, CancellationToken ct);

    Task<bool> TemplateCodeExistsAsync(string code, CancellationToken ct);

    /// <summary>Submitted letters stranded beyond the materialization window,
    /// oldest first (reconciliation candidates).</summary>
    Task<IReadOnlyList<StuckSubmissionRow>> ListStuckSubmissionsAsync(DateTime olderThan, int maxRows, CancellationToken ct);
}

/// <summary>
/// Write side: aggregate persistence with transactional outbox capture and
/// the sole guarded purge path. Submission allocates the per-unit yearly
/// reference number under a transactional advisory lock inside the same
/// database transaction that persists the letter and its outbox message.
/// </summary>
public interface ILetterJournal
{
    /// <summary>Loads the aggregate with children for mutation.</summary>
    Task<Letter?> FindTrackedLetterAsync(Guid id, CancellationToken ct);

    Task SaveAsync(Letter letter, CancellationToken ct);

    /// <summary>Persists any outbox messages buffered on the current context
    /// (used by the reconciliation re-publication flow).</summary>
    Task FlushOutboxAsync(CancellationToken ct);

    /// <summary>Atomically: advisory-locks the unit/year sequence, allocates
    /// the next reference number, applies the submission transition, persists
    /// the letter plus history and flushes the outbox-captured
    /// <c>LetterSubmittedDomainEvent</c> in one transaction.</summary>
    Task SubmitAsync(
        Letter letter,
        string retentionClass,
        DateTime? retentionExpiresOn,
        Func<CancellationToken, Task> publishSubmittedEvent,
        CancellationToken ct);

    Task SaveTemplateAsync(Template letterTemplate, CancellationToken ct);

    Task<bool> ExistsActiveHoldAsync(Guid letterId, CancellationToken ct);

    Task SaveHoldsAsync(IReadOnlyList<LetterHold> holds, CancellationToken ct);

    Task SaveHoldReleaseAsync(LetterHold hold, CancellationToken ct);

    Task SaveExportActivityAsync(ExportActivity activity, CancellationToken ct);

    /// <summary>Selects up to <paramref name="maxBatchSize"/> expired unheld
    /// letters, appends per-letter purge tombstones, and deletes exactly that
    /// batch in one guarded transaction (SET LOCAL trigger guard). Returns how
    /// many rows were removed and the remaining eligible estimate.</summary>
    Task<PurgeBatchResult> PurgeExpiredBatchAsync(int maxBatchSize, DateTime asOf, CancellationToken ct);
}

public sealed record PurgeBatchResult(int PurgedCount, int RemainingExpired);

/// <summary>
/// Deployment retention policy (ADR-028 decision 13): resolves a letter's
/// category to its retention class and expiry. Unknown classes retain
/// indefinitely.
/// </summary>
public interface IRetentionPolicy
{
    RetentionAssignment Assign(string categoryCode, DateTime submittedOn);
}

public sealed record RetentionAssignment(string Class, DateTime? ExpiresOn);
