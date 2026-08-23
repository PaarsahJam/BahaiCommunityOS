using CommunityOS.Correspondence.Application;
using CommunityOS.Correspondence.Domain;
using CommunityOS.Correspondence.Domain.Exceptions;
using CommunityOS.Correspondence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CommunityOS.Correspondence.Infrastructure;

/// <summary>
/// Write-side implementation. Every save is a single SaveChanges so
/// outbox-captured integration events commit atomically with the letter change
/// (ADR-015). Submission allocates the per-unit yearly reference under a
/// transactional advisory lock inside the same database transaction that
/// persists the letter and its outbox message (ADR-028 decision 4); the unique
/// constraint remains the storage-level backstop. The retention purge runs in
/// one explicit transaction that flips the session-local trigger guard,
/// appends tombstone history rows and deletes exactly the selected batch
/// (ADR-028 decision 13).
/// </summary>
public sealed class LetterJournal(CorrespondenceDbContext db, ILogger<LetterJournal> logger) : ILetterJournal
{
    public Task<Letter?> FindTrackedLetterAsync(Guid id, CancellationToken ct) =>
        db.Letters
            .Include(l => l.Recipients)
            .Include(l => l.DocumentLinks)
            .Include(l => l.Attachments)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task SaveAsync(Letter letter, CancellationToken ct)
    {
        db.Letters.Update(letter);
        PersistPendingHistory(letter);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>History is not a mapped navigation (FK-less table by design);
    /// pending aggregate rows are attached explicitly before each save.</summary>
    private void PersistPendingHistory(Letter letter)
    {
        var pending = letter.DrainPendingHistory();
        if (pending.Count > 0)
        {
            db.Set<LetterStatusHistory>().AddRange(pending);
        }
    }

    public Task FlushOutboxAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task SubmitAsync(
        Letter letter,
        string retentionClass,
        DateTime? retentionExpiresOn,
        Func<CancellationToken, Task> publishSubmittedEvent,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var year = now.Year;
        var submittedBy = letter.SubmittedBy ?? throw new InvalidOperationException("Submitter missing.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Serialize concurrent submissions for this unit/year.
            await db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(@key)",
                [new NpgsqlParameter<long>("key", StableLockKey(letter.OrganizationUnitId, year))],
                ct);

            var next = await NextSequenceAsync(letter.OrganizationUnitId, year, ct);
            letter.Submit(submittedBy, year, next, retentionClass, retentionExpiresOn, now);

            db.Letters.Add(letter);
            PersistPendingHistory(letter);
            await db.SaveChangesAsync(ct);

            // The bus outbox buffers the publish into this context; this second
            // SaveChanges flushes it inside the same transaction.
            await publishSubmittedEvent(ct);
            await db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
            logger.LetterSubmitted(letter.Id, year, next);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw new LetterConflictException(
                "The reference number could not be allocated; retry the submission.");
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<int> NextSequenceAsync(Guid organizationUnitId, int year, CancellationToken ct)
    {
        var max = await db.Letters
            .Where(l => l.OrganizationUnitId == organizationUnitId &&
                        l.LetterYear == year &&
                        l.LetterSequence != null)
            .Select(l => l.LetterSequence!.Value)
            .ToListAsync(ct);
        return max.Count == 0 ? 1 : max.Max() + 1;
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static long StableLockKey(Guid unitId, int year)
    {
        Span<byte> bytes = stackalloc byte[16];
        unitId.TryWriteBytes(bytes);
        unchecked
        {
            long hash = 17;
            foreach (var b in bytes)
            {
                hash = hash * 31 + b;
            }

            return (hash * 31 + year) & 0x7FFFFFFFFFFFFFFF;
        }
    }

    public async Task SaveTemplateAsync(Template letterTemplate, CancellationToken ct)
    {
        if (db.Entry(letterTemplate).State == EntityState.Detached)
        {
            db.Templates.Add(letterTemplate);
        }
        else
        {
            db.Templates.Update(letterTemplate);
        }

        await db.SaveChangesAsync(ct);
    }

    public Task<bool> ExistsActiveHoldAsync(Guid letterId, CancellationToken ct) =>
        db.LetterHolds.AnyAsync(h => h.LetterId == letterId && h.ReleasedOn == null, ct);

    public async Task SaveHoldsAsync(IReadOnlyList<LetterHold> holds, CancellationToken ct)
    {
        db.LetterHolds.AddRange(holds);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveHoldReleaseAsync(LetterHold hold, CancellationToken ct)
    {
        db.LetterHolds.Update(hold);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveExportActivityAsync(ExportActivity activity, CancellationToken ct)
    {
        db.ExportActivities.Add(activity);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Purge path: (1) flip the session-local guard, (2) select the
    /// bounded expired-unheld batch, (3) append per-letter tombstone history
    /// rows, (4) delete exactly those letters, (5) commit. History has no FK to
    /// letters, so tombstones survive the cascade.</summary>
    public async Task<PurgeBatchResult> PurgeExpiredBatchAsync(int maxBatchSize, DateTime asOf, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Session-local guard flip: only this transaction may delete.
            await db.Database.ExecuteSqlRawAsync(
                "SET LOCAL app.correspondence_purge_authorized = 'on'", ct);

            var expired = await db.Letters
                .Where(l => l.RetentionExpiresOn != null && l.RetentionExpiresOn <= asOf)
                .Where(l => !db.LetterHolds.Any(h => h.LetterId == l.Id && h.ReleasedOn == null))
                .OrderBy(l => l.RetentionExpiresOn).ThenBy(l => l.Id)
                .Take(maxBatchSize)
                .Select(l => new { l.Id, l.Status })
                .ToListAsync(ct);

            if (expired.Count == 0)
            {
                await tx.CommitAsync(ct);
                return new PurgeBatchResult(0, 0);
            }

            // Tombstones are persisted BEFORE the deletes, in the same
            // transaction: every purged letter's trail ends with its marker.
            var tombstones = expired
                .Select(e => LetterStatusHistory.CreateTombstone(e.Id, e.Status, asOf))
                .ToList();
            db.Set<LetterStatusHistory>().AddRange(tombstones);
            await db.SaveChangesAsync(ct);

            var ids = expired.Select(e => e.Id).ToArray();
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM correspondence.letters WHERE id = ANY(@ids)",
                [new NpgsqlParameter<Guid[]>("ids", ids)],
                ct);

            logger.PurgeBatchExecuted(expired.Count);

            var remaining = await CountExpiredUnheld(asOf, ct);
            await tx.CommitAsync(ct);
            return new PurgeBatchResult(expired.Count, remaining);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private Task<int> CountExpiredUnheld(DateTime asOfUtc, CancellationToken ct) =>
        db.Letters
            .Where(l => l.RetentionExpiresOn != null && l.RetentionExpiresOn <= asOfUtc)
            .Where(l => !db.LetterHolds.Any(h => h.LetterId == l.Id && h.ReleasedOn == null))
            .CountAsync(ct);
}

internal static partial class CorrespondenceJournalLog
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Letter {LetterId} submitted with reference year {LetterYear} sequence {LetterSequence}.")]
    public static partial void LetterSubmitted(this ILogger logger, Guid letterId, int letterYear, int letterSequence);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Retention purge batch executed: {PurgedCount} letters removed.")]
    public static partial void PurgeBatchExecuted(this ILogger logger, int purgedCount);
}
