using CommunityOS.Audit.Application;
using CommunityOS.Audit.Domain;
using CommunityOS.Audit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Audit.Infrastructure;

/// <summary>
/// Append-only store implementation (ADR-027 decision 10). Ingestion applies
/// the retention assignment, enforces hash idempotency and persists; there is
/// deliberately no update or delete path outside the guarded purge batch.
/// </summary>
public sealed class AuditIngestor(AuditDbContext db, IRetentionPolicy retention, ILogger<AuditIngestor> logger)
    : IAuditIngestor
{
    public async Task<IngestOutcome> IngestAsync(IngestCandidate candidate, CancellationToken ct)
    {
        if (await db.AuditEntries.AnyAsync(e => e.SourceEventHash == candidate.SourceEventHash, ct))
        {
            logger.DuplicateSuppressed(candidate.SourceEventType, candidate.SourceEventHash);
            return IngestOutcome.Duplicate;
        }

        var assignment = retention.Assign(candidate.SourceEventType, candidate.OccurredOn);
        var entry = AuditEntry.Create(
            candidate.SourceService,
            candidate.SourceEventType,
            candidate.Action,
            candidate.SourceEventHash,
            candidate.ResourceType,
            candidate.ResourceId,
            candidate.OccurredOn,
            DateTime.UtcNow,
            candidate.Sensitivity,
            assignment.Class,
            outcome: candidate.Outcome,
            secondaryResourceId: candidate.SecondaryResourceId,
            subjectId: candidate.SubjectId,
            actorId: candidate.ActorId,
            organizationUnitId: candidate.OrganizationUnitId,
            metadata: candidate.Metadata,
            retentionExpiresOn: assignment.ExpiresOn);

        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        logger.EntryPersisted(entry.Id, candidate.SourceEventType);
        return IngestOutcome.Persisted;
    }

    public Task<bool> ExistsByHashAsync(string sourceEventHash, CancellationToken ct) =>
        db.AuditEntries.AnyAsync(e => e.SourceEventHash == sourceEventHash, ct);

    public async Task AppendAsync(AuditEntry entry, CancellationToken ct)
    {
        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }
}

internal static partial class AuditInfrastructureLog
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Duplicate audit event suppressed by hash identity: {SourceType} {SourceEventHash}.")]
    public static partial void DuplicateSuppressed(this ILogger logger, string sourceType, string sourceEventHash);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Audit entry {EntryId} persisted for {SourceType}.")]
    public static partial void EntryPersisted(this ILogger logger, Guid entryId, string sourceType);
}
