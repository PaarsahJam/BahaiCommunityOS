using CommunityOS.Audit.Application;
using CommunityOS.Audit.Domain;
using CommunityOS.Audit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Audit.Infrastructure;

/// <summary>
/// Read-side implementation. All queries order by (occurred_on, id) so paging
/// is deterministic; authorization filtering happens in the application layer
/// on the returned rows (ADR-027 decision 13).
/// </summary>
public sealed class AuditReader(AuditDbContext db) : IAuditReader
{
    public async Task<IReadOnlyList<AuditEntryRow>> QueryAsync(
        AuditQueryFilters filters,
        bool ascending,
        int offset,
        int maxRows,
        CancellationToken ct)
    {
        var query = BuildFilterQuery(filters);

        query = ascending
            ? query.OrderBy(e => e.OccurredOn).ThenBy(e => e.Id)
            : query.OrderByDescending(e => e.OccurredOn).ThenByDescending(e => e.Id);

        return await query
            .Skip(offset)
            .Take(maxRows)
            .Select(row => new AuditEntryRow(
                row.Id, row.SourceService, row.SourceEventType, row.Action, row.Outcome,
                row.ResourceType, row.ResourceId, row.SecondaryResourceId, row.SubjectId, row.ActorId,
                row.OrganizationUnitId, row.Sensitivity, row.MetadataJson, row.RetentionClass,
                row.RetentionExpiresOn, row.OccurredOn, row.IngestedOn))
            .ToListAsync(ct);
    }

    public async Task<AuditEntryRow?> FindAsync(Guid id, CancellationToken ct)
    {
        return await db.AuditEntries
            .Where(e => e.Id == id)
            .Select(row => new AuditEntryRow(
                row.Id, row.SourceService, row.SourceEventType, row.Action, row.Outcome,
                row.ResourceType, row.ResourceId, row.SecondaryResourceId, row.SubjectId, row.ActorId,
                row.OrganizationUnitId, row.Sensitivity, row.MetadataJson, row.RetentionClass,
                row.RetentionExpiresOn, row.OccurredOn, row.IngestedOn))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<AuditEntryRow>> FindRangeAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];

        var rows = await db.AuditEntries
            .Where(e => ids.Contains(e.Id))
            .Select(row => new AuditEntryRow(
                row.Id, row.SourceService, row.SourceEventType, row.Action, row.Outcome,
                row.ResourceType, row.ResourceId, row.SecondaryResourceId, row.SubjectId, row.ActorId,
                row.OrganizationUnitId, row.Sensitivity, row.MetadataJson, row.RetentionClass,
                row.RetentionExpiresOn, row.OccurredOn, row.IngestedOn))
            .ToListAsync(ct);
        return rows;
    }

    public Task<bool> HasActiveHoldAsync(Guid entryId, CancellationToken ct) =>
        db.AuditEntryHolds.AnyAsync(h => h.EntryId == entryId && h.ReleasedOn == null, ct);

    public Task<AuditEntryHold?> FindHoldAsync(Guid holdId, CancellationToken ct) =>
        db.AuditEntryHolds.FirstOrDefaultAsync(h => h.Id == holdId, ct);

    public Task<int> CountExpiredUnheldAsync(DateTime asOf, CancellationToken ct) =>
        CountExpiredUnheld(asOf, ct);

    internal IQueryable<AuditEntry> BuildFilterQuery(AuditQueryFilters filters)
    {
        var query = db.AuditEntries.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.SourceService))
        {
            query = query.Where(e => e.SourceService == filters.SourceService);
        }

        if (!string.IsNullOrWhiteSpace(filters.EventType))
        {
            query = query.Where(e => e.SourceEventType == filters.EventType);
        }

        if (!string.IsNullOrWhiteSpace(filters.Action))
        {
            query = query.Where(e => e.Action == filters.Action);
        }

        if (!string.IsNullOrWhiteSpace(filters.ResourceType))
        {
            query = query.Where(e => e.ResourceType == filters.ResourceType);
            if (filters.ResourceId is { } resourceId)
            {
                query = query.Where(e => e.ResourceId == resourceId);
            }
        }

        if (filters.SubjectId is { } subjectId)
        {
            query = query.Where(e => e.SubjectId == subjectId);
        }

        if (filters.ActorId is { } actorId)
        {
            query = query.Where(e => e.ActorId == actorId);
        }

        if (filters.OrganizationUnitId is { } unitId)
        {
            query = query.Where(e => e.OrganizationUnitId == unitId);
        }

        if (filters.OccurredFrom is { } from)
        {
            query = query.Where(e => e.OccurredOn >= from.ToUniversalTime());
        }

        if (filters.OccurredTo is { } to)
        {
            query = query.Where(e => e.OccurredOn <= to.ToUniversalTime());
        }

        return query;
    }

    private Task<int> CountExpiredUnheld(DateTime asOfUtc, CancellationToken ct) =>
        db.AuditEntries
            .Where(e => e.RetentionExpiresOn != null && e.RetentionExpiresOn <= asOfUtc)
            .Where(e => !db.AuditEntryHolds.Any(h => h.EntryId == e.Id && h.ReleasedOn == null))
            .CountAsync(ct);

    internal static readonly Func<AuditEntry, AuditEntryRow> ToRow = row => new(
        row.Id, row.SourceService, row.SourceEventType, row.Action, row.Outcome,
        row.ResourceType, row.ResourceId, row.SecondaryResourceId, row.SubjectId, row.ActorId,
        row.OrganizationUnitId, row.Sensitivity, row.MetadataJson, row.RetentionClass,
        row.RetentionExpiresOn, row.OccurredOn, row.IngestedOn);
}
