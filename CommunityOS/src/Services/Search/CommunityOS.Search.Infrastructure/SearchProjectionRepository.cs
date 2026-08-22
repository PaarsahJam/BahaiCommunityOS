using CommunityOS.Search.Application;
using CommunityOS.Search.Domain;
using CommunityOS.Search.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CommunityOS.Search.Infrastructure;
public sealed class SearchProjectionRepository(SearchDbContext db, IOptions<SearchOptions> options) : ISearchProjectionRepository
{
    // Whitelisted text-search configurations: the value is interpolated into
    // to_tsvector/websearch_to_tsquery calls, so it must never be free-form.
    private static readonly string[] AllowedLanguages = ["english", "simple"];

    public async Task<IReadOnlyList<SearchProjectionRow>> FetchCandidatesAsync(SearchFilters filters, int offset, int maxRows, CancellationToken ct)
    {
        var q = filters.Query.Trim();
        if (q.Length == 0) throw new ArgumentException("Query is required.");
        var language = ResolveLanguage();

        await using var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.id, d.source_type, d.source_id, d.display_title, d.type_code, d.status,
                   d.is_sensitive, d.organization_unit_id, d.indexed_on, d.created_on,
                   ts_rank(d.search_vector, websearch_to_tsquery(@language, @query)) AS rank,
                   COALESCE((SELECT array_agg(s.organization_unit_id)
                             FROM search.search_document_scopes s
                             WHERE s.search_document_id = d.id), '{}') AS additional_scopes
            FROM search.search_documents d
            WHERE d.source_type = ANY(@types)
              AND d.search_vector @@ websearch_to_tsquery(@language, @query)
              AND (@sensitive OR NOT d.is_sensitive)
              AND (@unit IS NULL OR d.organization_unit_id = @unit OR EXISTS
                   (SELECT 1 FROM search.search_document_scopes s
                    WHERE s.search_document_id = d.id AND s.organization_unit_id = @unit))
              AND (@statuses IS NULL OR d.status = ANY(@statuses))
              AND (@statuses IS NOT NULL OR d.status NOT IN ('Deactivated','Archived','Merged','Cancelled'))
            ORDER BY rank DESC, d.indexed_on DESC, d.id
            OFFSET @offset LIMIT @limit
            """;
        command.Parameters.AddWithValue("language", language);
        command.Parameters.AddWithValue("query", q);
        command.Parameters.AddWithValue("types", filters.SourceTypes.ToArray());
        command.Parameters.AddWithValue("sensitive", filters.IncludeSensitive);
        command.Parameters.AddWithValue("unit", (object?)filters.OrganizationUnitId ?? DBNull.Value);
        command.Parameters.AddWithValue("statuses", (object?)filters.Statuses?.ToArray() ?? DBNull.Value);
        command.Parameters.AddWithValue("offset", offset);
        command.Parameters.AddWithValue("limit", maxRows);

        var rows = new List<SearchProjectionRow>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var scopes = reader.GetFieldValue<Guid[]>(11);
            rows.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetBoolean(6),
                reader.IsDBNull(7) ? null : reader.GetGuid(7),
                scopes.Length == 0 ? [] : scopes,
                reader.GetDateTime(8), reader.GetDateTime(9), reader.GetDouble(10)));
        }
        return rows;
    }

    public async Task<SearchIndexHealthDto> GetHealthAsync(CancellationToken ct)
    {
        var sources = await db.SearchIndexLogs.AsNoTracking().Select(x => new SearchSourceHealthDto(x.SourceType, db.SearchDocuments.LongCount(y => y.SourceType == x.SourceType), x.LastEventOccurredOn, x.IndexedCount)).ToListAsync(ct);
        return new(sources, DateTime.UtcNow);
    }

    private string ResolveLanguage()
    {
        var configured = options.Value.DefaultLanguage;
        return AllowedLanguages.Contains(configured, StringComparer.OrdinalIgnoreCase)
            ? configured.ToLowerInvariant()
            : throw new InvalidOperationException($"Unsupported Search:DefaultLanguage '{configured}'.");
    }
}
