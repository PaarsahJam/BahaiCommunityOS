using CommunityOS.Localization.Application;
using CommunityOS.Localization.Domain;
using CommunityOS.Localization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Localization.Infrastructure;

/// <summary>
/// Read-side implementation. Deterministic ordering everywhere; list surfaces
/// are metadata-only projections (values never leave the store on walks —
/// ADR-029 decision 10). Aggregates needed for mutation are loaded TRACKED so
/// the journal's single SaveChanges captures their changes.
/// </summary>
public sealed class LocalizationReader(LocalizationDbContext db) : ILocalizationReader
{
    public async Task<IReadOnlyList<Locale>> ListLocalesAsync(CancellationToken ct) =>
        await db.Locales.AsNoTracking().OrderBy(l => l.Code).ToListAsync(ct);

    public Task<Locale?> FindLocaleAsync(string code, CancellationToken ct) =>
        db.Locales.FirstOrDefaultAsync(l => l.Code == code, ct);

    public Task<Locale?> FindDefaultLocaleAsync(CancellationToken ct) =>
        db.Locales.AsNoTracking().FirstOrDefaultAsync(l => l.IsDefault, ct);

    public Task<bool> LocaleCodeExistsAsync(string code, CancellationToken ct) =>
        db.Locales.AnyAsync(l => l.Code == code, ct);

    public Task<ResourceNamespace?> FindNamespaceAsync(Guid id, CancellationToken ct) =>
        db.Namespaces.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<ResourceNamespace?> FindNamespaceByNameAsync(string name, CancellationToken ct)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return await db.Namespaces.FirstOrDefaultAsync(n => n.Name == normalized, ct);
    }

    public async Task<IReadOnlyList<NamespaceRow>> ListNamespacesAsync(CancellationToken ct) =>
        await db.Namespaces.AsNoTracking()
            .OrderBy(n => n.Name)
            .Select(n => new NamespaceRow(n.Id, n.Name, n.Description, n.CreatedOn))
            .ToListAsync(ct);

    public Task<ResourceEntry?> FindTrackedEntryAsync(Guid id, CancellationToken ct) =>
        db.Entries.Include(e => e.Revisions).FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<ResourceEntry?> FindTrackedEntryByKeyAsync(Guid namespaceId, string key, CancellationToken ct) =>
        db.Entries.Include(e => e.Revisions)
            .FirstOrDefaultAsync(e => e.NamespaceId == namespaceId && e.Key == key.Trim(), ct);

    public Task<string?> FindNamespaceNameAsync(Guid namespaceId, CancellationToken ct) =>
        db.Namespaces.Where(n => n.Id == namespaceId).Select(n => n.Name).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<EntryRow>> WalkEntriesAsync(
        Guid? namespaceId,
        string? stateFilter,
        string? search,
        (string NamespaceName, string Key, Guid Id)? afterKey,
        int maxRows,
        CancellationToken ct)
    {
        var rows = db.Entries
            .AsNoTracking()
            .Join(db.Namespaces,
                e => e.NamespaceId, n => n.Id,
                (e, n) => new { e, n })
            .OrderBy(x => x.n.Name).ThenBy(x => x.e.Key).ThenBy(x => x.e.Id);

        if (namespaceId.HasValue)
        {
            rows = rows.Where(x => x.e.NamespaceId == namespaceId.Value)
                .OrderBy(x => x.n.Name).ThenBy(x => x.e.Key).ThenBy(x => x.e.Id);
        }

        // State filtering happens IN the database query so keyset pagination
        // stays complete: Take() must never precede the filter.
        if (!string.IsNullOrWhiteSpace(stateFilter))
        {
            if (stateFilter == "deprecated")
            {
                rows = rows.Where(x => x.e.IsDeprecated)
                    .OrderBy(x => x.n.Name).ThenBy(x => x.e.Key).ThenBy(x => x.e.Id);
            }
            else
            {
                // The API contract accepts snake_case tokens ("in_review");
                // enum names are PascalCase, so separators are stripped before
                // parsing (validated whitelist guarantees a known token).
                var wanted = System.Enum.Parse<ReviewState>(
                    stateFilter.Replace("-", string.Empty).Replace("_", string.Empty),
                    ignoreCase: true);
                rows = rows.Where(x => db.Set<ResourceRevision>()
                        .Any(r => r.EntryId == x.e.Id && r.State == wanted))
                    .OrderBy(x => x.n.Name).ThenBy(x => x.e.Key).ThenBy(x => x.e.Id);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            rows = rows.Where(x => EF.Functions.ILike(x.e.Key, $"%{EscapeLike(search)}%"))
                .OrderBy(x => x.n.Name).ThenBy(x => x.e.Key).ThenBy(x => x.e.Id);
        }

        if (afterKey.HasValue)
        {
            var (nsName, key, id) = afterKey.Value;
            rows = rows.Where(x =>
                    string.Compare(x.n.Name, nsName, StringComparison.Ordinal) > 0 ||
                    (x.n.Name == nsName && string.Compare(x.e.Key, key, StringComparison.Ordinal) > 0) ||
                    (x.n.Name == nsName && x.e.Key == key && x.e.Id.CompareTo(id) > 0))
                .OrderBy(x => x.n.Name).ThenBy(x => x.e.Key).ThenBy(x => x.e.Id);
        }

        var page = await rows.Take(maxRows)
            .Select(x => new { x.e.Id, NamespaceId = x.n.Id, NamespaceName = x.n.Name, x.e.Key,
                x.e.IsDeprecated, x.e.DeprecatedOn })
            .ToListAsync(ct);

        if (page.Count == 0)
        {
            return [];
        }

        var entryIds = page.Select(p => p.Id).ToList();
        var revisions = await db.Set<ResourceRevision>().AsNoTracking()
            .Where(r => entryIds.Contains(r.EntryId))
            .OrderBy(r => r.ProposedOn).ThenBy(r => r.Id)
            .Select(r => new { r.Id, r.EntryId, r.CultureCode, r.State, r.Provenance,
                r.ProposedBy, r.ProposedOn, r.ReviewedBy, r.ReviewedOn })
            .ToListAsync(ct);

        var result = new List<EntryRow>(page.Count);
        foreach (var p in page)
        {
            result.Add(new EntryRow(
                p.Id, p.NamespaceId, p.NamespaceName, p.Key,
                p.IsDeprecated, p.DeprecatedOn,
                revisions.Where(r => r.EntryId == p.Id)
                    .Select(r => new EntryRevisionRow(
                        r.Id, r.CultureCode, r.State, r.Provenance,
                        r.ProposedBy, r.ProposedOn, r.ReviewedBy, r.ReviewedOn))
                    .ToList()));
        }

        return result;
    }

    public async Task<IReadOnlyList<ExportCandidateRow>> LoadExportCandidatesAsync(
        Guid? namespaceId, IReadOnlyList<string> cultures, int maxRows, CancellationToken ct)
    {
        var cultureSet = cultures.Select(Bcp47.Normalize).ToHashSet(StringComparer.Ordinal);
        var query = from e in db.Entries.AsNoTracking()
            join n in db.Namespaces.AsNoTracking() on e.NamespaceId equals n.Id
            join r in db.Set<ResourceRevision>().AsNoTracking() on e.Id equals r.EntryId into revisions
            where !e.IsDeprecated
            select new { e.Id, e.NamespaceId, n.Name, e.Key, Revisions = revisions };

        if (namespaceId.HasValue)
        {
            query = query.Where(x => x.NamespaceId == namespaceId.Value);
        }

        var raw = await query.ToListAsync(ct);

        var candidates = raw
            .OrderBy(x => x.Name).ThenBy(x => x.Key).ThenBy(x => x.Id)
            .Take(maxRows);

        var result = new List<ExportCandidateRow>();
        foreach (var row in candidates)
        {
            var approved = row.Revisions
                .Where(r => r.State == ReviewState.Approved && cultureSet.Contains(r.CultureCode))
                .GroupBy(r => r.CultureCode, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.Ordinal);
            if (approved.Count > 0)
            {
                result.Add(new ExportCandidateRow(row.Id, row.Name, row.Key, approved));
            }
        }

        return result;
    }

    public Task<EntityTranslation?> FindTrackedEntityTranslationAsync(Guid id, CancellationToken ct) =>
        db.EntityTranslations.Include(t => t.Revisions).FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<EntityTranslation?> FindEntityTranslationAsync(
        string sourceContext, string entityType, Guid entityId, string field, string culture, CancellationToken ct) =>
        db.EntityTranslations.Include(t => t.Revisions).FirstOrDefaultAsync(t =>
            t.SourceContext == sourceContext &&
            t.EntityType == entityType &&
            t.EntityId == entityId &&
            t.Field == field &&
            t.CultureCode == Bcp47.Normalize(culture), ct);

    public async Task<IReadOnlyList<EntityTranslationRow>> QueryEntityTranslationsAsync(
        IReadOnlyList<EntityTranslationRef> refs, bool includePending, int maxRows, CancellationToken ct)
    {
        if (refs.Count == 0)
        {
            return [];
        }

        var normalized = refs
            .Select(r => (
                SourceContext: r.SourceContext.Trim().ToLowerInvariant(),
                EntityType: r.EntityType.Trim().ToLowerInvariant(),
                r.EntityId,
                Field: r.Field.Trim().ToLowerInvariant(),
                Culture: Bcp47.Normalize(r.Culture)))
            .Distinct()
            .ToList();

        var sourceContexts = normalized.Select(n => n.SourceContext).Distinct().ToList();
        var entityIds = normalized.Select(n => n.EntityId).Distinct().ToList();

        var aggregates = await db.EntityTranslations.AsNoTracking()
            .Include(t => t.Revisions)
            .Where(t => sourceContexts.Contains(t.SourceContext) && entityIds.Contains(t.EntityId))
            .Take(maxRows * 2)
            .ToListAsync(ct);

        var wanted = normalized.ToHashSet(OrdinalTupleComparer.Instance);
        var rows = new List<EntityTranslationRow>();
        foreach (var aggregate in aggregates)
        {
            if (!wanted.Contains((aggregate.SourceContext, aggregate.EntityType,
                    aggregate.EntityId, aggregate.Field, aggregate.CultureCode)) ||
                aggregate.IsDeprecated)
            {
                continue;
            }

            var approved = aggregate.CurrentApproved();
            if (approved is not null)
            {
                rows.Add(new EntityTranslationRow(aggregate.Id, aggregate.SourceContext,
                    aggregate.EntityType, aggregate.EntityId, aggregate.Field,
                    aggregate.CultureCode, approved.State, approved.Value, approved.ReviewedOn));
                continue;
            }

            if (includePending)
            {
                var latest = aggregate.Revisions.OrderBy(r => r.ProposedOn).ThenBy(r => r.Id).Last();
                rows.Add(new EntityTranslationRow(aggregate.Id, aggregate.SourceContext,
                    aggregate.EntityType, aggregate.EntityId, aggregate.Field,
                    aggregate.CultureCode, latest.State,
                    latest.State == ReviewState.Draft || latest.State == ReviewState.InReview
                        ? latest.Value
                        : null, latest.ReviewedOn));
            }
        }

        return rows;
    }

    public Task<TranslationSuggestion?> FindSuggestionAsync(Guid id, CancellationToken ct) =>
        db.Suggestions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<SuggestionRow>> ListSuggestionsAsync(
        string? statusFilter, int maxRows, CancellationToken ct)
    {
        var query = db.Suggestions.AsNoTracking()
            .OrderByDescending(s => s.CreatedOn).ThenBy(s => s.Id);
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            var status = System.Enum.Parse<SuggestionStatus>(statusFilter, ignoreCase: true);
            return await query.Where(s => s.Status == status)
                .Take(maxRows)
                .Select(s => new SuggestionRow(
                    s.Id, s.TargetKind, s.TargetId, s.TargetCultureCode,
                    s.Status, s.Provenance, s.CreatedOn)
                {
                    SuggestedValue = s.SuggestedValue
                })
                .ToListAsync(ct);
        }

        return await query
            .Take(maxRows)
            .Select(s => new SuggestionRow(
                s.Id, s.TargetKind, s.TargetId, s.TargetCultureCode,
                s.Status, s.Provenance, s.CreatedOn)
            {
                SuggestedValue = s.SuggestedValue
            })
            .ToListAsync(ct);
    }

    public async Task<long> GetCatalogVersionAsync(CancellationToken ct)
    {
        var state = await db.CatalogState.AsNoTracking().SingleAsync(ct);
        return state.Version;
    }

    private static string EscapeLike(string input) =>
        input.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}

/// <summary>Exact-ordinal comparer for normalized by-reference tuples.</summary>
internal sealed class OrdinalTupleComparer : IEqualityComparer<
    (string SourceContext, string EntityType, Guid EntityId, string Field, string Culture)>
{
    public static readonly OrdinalTupleComparer Instance = new();

    public bool Equals(
        (string SourceContext, string EntityType, Guid EntityId, string Field, string Culture) x,
        (string SourceContext, string EntityType, Guid EntityId, string Field, string Culture) y) =>
        x.SourceContext.Equals(y.SourceContext, StringComparison.Ordinal) &&
        x.EntityType.Equals(y.EntityType, StringComparison.Ordinal) &&
        x.EntityId.Equals(y.EntityId) &&
        x.Field.Equals(y.Field, StringComparison.Ordinal) &&
        x.Culture.Equals(y.Culture, StringComparison.Ordinal);

    public int GetHashCode(
        (string SourceContext, string EntityType, Guid EntityId, string Field, string Culture) obj) =>
        HashCode.Combine(obj.SourceContext, obj.EntityType, obj.EntityId, obj.Field, obj.Culture);
}
