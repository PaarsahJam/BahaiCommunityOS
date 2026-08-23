using CommunityOS.Audit.Application;
using CommunityOS.Audit.Domain;
using CommunityOS.Audit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CommunityOS.Audit.Infrastructure;

/// <summary>
/// Journal write-side implementation. Hold placement/release run inside a
/// single SaveChanges (atomic). The retention purge runs in one explicit
/// transaction that (1) flips the session-local trigger guard, (2) persists
/// the marker entry, (3) deletes exactly the selected batch, and commits —
/// so a crash either keeps entries plus no marker, or removes them with the
/// marker present (ADR-027 decision 14).
/// </summary>
public sealed class AuditJournal(AuditDbContext db, ILogger<AuditJournal> logger) : IAuditJournal
{
    public async Task AppendAsync(AuditEntry entry, CancellationToken ct)
    {
        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task AppendWithHoldsAsync(AuditEntry journalEntry, IReadOnlyList<AuditEntryHold> holds, CancellationToken ct)
    {
        db.AuditEntries.Add(journalEntry);
        db.AuditEntryHolds.AddRange(holds);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateHoldAsync(AuditEntryHold hold, AuditEntry journalEntry, CancellationToken ct)
    {
        db.AuditEntryHolds.Update(hold);
        db.AuditEntries.Add(journalEntry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PurgeBatchResult> PurgeExpiredBatchAsync(
        int maxBatchSize,
        Func<int, IReadOnlyList<string>, AuditEntry> markerFactory,
        DateTime asOf,
        CancellationToken ct)
    {
        await using var tx = await BeginTransactionAsync(ct);
        try
        {
            // Session-local guard flip: only this transaction may delete.
            await db.Database.ExecuteSqlRawAsync(
                "SET LOCAL app.audit_purge_authorized = 'on'", ct);

            var expiredIds = await db.AuditEntries
                .Where(e => e.RetentionExpiresOn != null && e.RetentionExpiresOn <= asOf)
                .Where(e => !db.AuditEntryHolds.Any(h => h.EntryId == e.Id && h.ReleasedOn == null))
                .OrderBy(e => e.RetentionExpiresOn).ThenBy(e => e.Id)
                .Take(maxBatchSize)
                .Select(e => e.Id)
                .ToListAsync(ct);

            if (expiredIds.Count == 0)
            {
                await tx.CommitAsync(ct);
                return new PurgeBatchResult(0, 0);
            }

            var classes = await db.AuditEntries
                .Where(e => expiredIds.Contains(e.Id))
                .Select(e => e.RetentionClass)
                .Distinct()
                .ToListAsync(ct);

            // The marker is persisted BEFORE the deletes, in the same
            // transaction: the audit trail always ends with the tombstone.
            var marker = markerFactory(expiredIds.Count, classes);
            db.AuditEntries.Add(marker);
            await db.SaveChangesAsync(ct);

            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM audit.audit_entries WHERE id = ANY(@ids)",
                new NpgsqlParameter("ids", expiredIds.ToArray()),
                ct);

            logger.PurgeBatchExecuted(marker.Id, expiredIds.Count);

            var remaining = await CountExpiredUnheld(asOf, ct);
            await tx.CommitAsync(ct);
            return new PurgeBatchResult(expiredIds.Count, remaining);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct) =>
        db.Database.BeginTransactionAsync(ct);

    private Task<int> CountExpiredUnheld(DateTime asOf, CancellationToken ct) =>
        db.AuditEntries
            .Where(e => e.RetentionExpiresOn != null && e.RetentionExpiresOn <= asOf)
            .Where(e => !db.AuditEntryHolds.Any(h => h.EntryId == e.Id && h.ReleasedOn == null))
            .CountAsync(ct);
}

internal static partial class AuditJournalLog
{
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Retention purge batch executed: marker {MarkerEntryId}, {PurgedCount} entries removed.")]
    public static partial void PurgeBatchExecuted(this ILogger logger, Guid markerEntryId, int purgedCount);
}
