using CommunityOS.Search.Application;
using CommunityOS.Search.Domain;
using CommunityOS.Search.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Search.Infrastructure;
public interface ISearchProjectionWriter
{
    /// <summary>Returns true when the event was applied, false when it was rejected as stale.</summary>
    Task<bool> UpsertAsync(string sourceType, Guid sourceId, string? title, string? typeCode, string? status, bool? sensitive, Guid? unit, bool unitPresent, DateTime occurredOn, bool created, CancellationToken ct);
}
public sealed class SearchProjectionWriter(SearchDbContext db) : ISearchProjectionWriter
{
    public async Task<bool> UpsertAsync(string sourceType, Guid sourceId, string? title, string? typeCode, string? status, bool? sensitive, Guid? unit, bool unitPresent, DateTime occurredOn, bool created, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var doc = await db.SearchDocuments.Include(x => x.AdditionalScopes).SingleOrDefaultAsync(x => x.SourceType == sourceType && x.SourceId == sourceId, ct);
        if (doc is null) db.SearchDocuments.Add(SearchDocument.Create(sourceType, sourceId, title ?? typeCode ?? sourceType, typeCode ?? sourceType, status ?? "Unknown", sensitive ?? false, unit, occurredOn));
        else if (!doc.Apply(title, typeCode, status, sensitive, unit, unitPresent, occurredOn, created)) return false;
        await db.SaveChangesAsync(ct);

        // Create() mints a fresh aggregate id, so re-resolve it from the
        // natural key before updating the computed vector.
        var docId = doc?.Id ?? await db.SearchDocuments.Where(x => x.SourceType == sourceType && x.SourceId == sourceId).Select(x => x.Id).SingleAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE search.search_documents SET search_vector = to_tsvector('english', coalesce(display_title, '') || ' ' || coalesce(type_code, '')) WHERE id = {docId}", ct);

        // Index health bookkeeping (ADR-026): one log row per source type,
        // updated in the same transaction as the projection write. The last
        // event timestamp only ever moves forward; the applied-event count
        // increments once per accepted (non-stale) event.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO search.search_index_log (source_type, last_event_occurred_on, indexed_count)
            VALUES ({sourceType}, {occurredOn}, 1)
            ON CONFLICT (source_type) DO UPDATE SET
                last_event_occurred_on = GREATEST(search.search_index_log.last_event_occurred_on, EXCLUDED.last_event_occurred_on),
                indexed_count = search.search_index_log.indexed_count + 1
            """, ct);
        await tx.CommitAsync(ct);
        return true;
    }
}
