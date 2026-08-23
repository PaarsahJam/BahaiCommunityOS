using CommunityOS.Audit.Domain;

namespace CommunityOS.Audit.Application;

/// <summary>
/// Read side of the journal: candidate queries, single lookups, hold lookups
/// and retention-eligibility counting. Implementations must apply the query
/// filters and deterministic ordering at the store; visibility is decided by
/// the handlers through the Authorization service (ADR-027 decision 11).
/// </summary>
public interface IAuditReader
{
    /// <summary>Returns up to <paramref name="maxRows"/> candidates matching
    /// the filters, ordered by OccurredOn in the requested direction (then id)
    /// — the deterministic order the handler walks when authorizing.</summary>
    Task<IReadOnlyList<AuditEntryRow>> QueryAsync(
        AuditQueryFilters filters, bool ascending, int offset, int maxRows, CancellationToken ct);

    Task<AuditEntryRow?> FindAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<AuditEntryRow>> FindRangeAsync(IReadOnlyList<Guid> ids, CancellationToken ct);

    Task<bool> HasActiveHoldAsync(Guid entryId, CancellationToken ct);

    Task<AuditEntryHold?> FindHoldAsync(Guid holdId, CancellationToken ct);

    Task<int> CountExpiredUnheldAsync(DateTime asOf, CancellationToken ct);
}

/// <summary>
/// Ingestion boundary for producer events: hash-based duplicate detection and
/// append-only persistence. There is no update path.
/// </summary>
public interface IAuditIngestor
{
    /// <summary>Applies the retention policy, checks the hash identity and
    /// persists; a duplicate resolves to a success no-op (ADR-027 decision
    /// 10).</summary>
    Task<IngestOutcome> IngestAsync(IngestCandidate candidate, CancellationToken ct);

    Task<bool> ExistsByHashAsync(string sourceEventHash, CancellationToken ct);

    Task AppendAsync(AuditEntry entry, CancellationToken ct);
}

/// <summary>
/// Administrative journal operations: audit-of-audit appends, hold placement
/// with their journal entry in one transaction, hold release bookkeeping and
/// the sole guarded purge path. The purge writes its marker entry inside the
/// transaction before deleting the bounded batch under the database trigger
/// guard (ADR-027 decisions 2 and 14).
/// </summary>
public interface IAuditJournal
{
    Task AppendAsync(AuditEntry entry, CancellationToken ct);

    Task AppendWithHoldsAsync(AuditEntry journalEntry, IReadOnlyList<AuditEntryHold> holds, CancellationToken ct);

    Task UpdateHoldAsync(AuditEntryHold hold, AuditEntry journalEntry, CancellationToken ct);

    /// <summary>Selects up to <paramref name="maxBatchSize"/> expired unheld
    /// entries, invokes the factory to build the purge-marker entry carrying
    /// the exact batch count/classes, persists the marker and deletes the batch
    /// in one guarded transaction. Returns how many rows were removed and the
    /// remaining eligible estimate.</summary>
    Task<PurgeBatchResult> PurgeExpiredBatchAsync(
        int maxBatchSize,
        Func<int, IReadOnlyList<string>, AuditEntry> markerFactory,
        DateTime asOf,
        CancellationToken ct);
}

/// <summary>
/// Deployment retention policy (ADR-027 decision 14): resolves an event type
/// to its retention class and expiry. Unknown classes retain indefinitely.
/// </summary>
public interface IRetentionPolicy
{
    RetentionAssignment Assign(string sourceEventType, DateTime occurredOn);
}

public sealed record RetentionAssignment(string Class, DateTime? ExpiresOn);
