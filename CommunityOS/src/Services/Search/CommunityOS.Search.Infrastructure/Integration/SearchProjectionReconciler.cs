using CommunityOS.Search.Application;
using CommunityOS.Search.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Search.Infrastructure.Integration;

/// <summary>
/// Rebuilds internal consistency of the projection store (ADR-026). The first
/// gate never reads back from the source services; "reindex" therefore means:
/// recompute every tsvector from the stored display title and type code
/// (repairing null/stale vectors) and resynchronize the per-source-type index
/// log counts with the actual document rows. Runs synchronously inside one
/// transaction serialized by a database advisory lock, so concurrent admin
/// calls cannot interleave.
/// </summary>
public sealed class SearchProjectionReconciler(SearchDbContext db) : IProjectionReconciler
{
    public async Task ReconcileAsync(string? sourceType, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(hashtext('communityos-search-reindex'))", ct);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE search.search_documents
            SET search_vector = to_tsvector('english', display_title || ' ' || type_code)
            WHERE {sourceType} IS NULL OR source_type = {sourceType}
            """, ct);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO search.search_index_log (source_type, last_event_occurred_on, indexed_count)
            SELECT d.source_type, MAX(d.indexed_on), COUNT(*)
            FROM search.search_documents d
            WHERE {sourceType} IS NULL OR d.source_type = {sourceType}
            GROUP BY d.source_type
            ON CONFLICT (source_type) DO UPDATE SET
                indexed_count = EXCLUDED.indexed_count,
                last_event_occurred_on = GREATEST(search.search_index_log.last_event_occurred_on, EXCLUDED.last_event_occurred_on)
            """, ct);

        await tx.CommitAsync(ct);
    }
}
