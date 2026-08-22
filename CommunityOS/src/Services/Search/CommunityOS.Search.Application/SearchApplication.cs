using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Search.Application.Authorization;
using CommunityOS.Search.Application.Permissions;
using CommunityOS.Search.Domain;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CommunityOS.Search.Application;

public sealed record SearchQuery(Guid ActorId, string Query, IReadOnlyList<string>? Types, string? SourceType,
    Guid? OrganizationUnitId, IReadOnlyList<string>? Statuses, bool Sensitive, int? Limit, int Offset) : IRequest<SearchResultPageDto>;
public sealed record SearchResultItemDto(Guid Id, string SourceType, Guid SourceId, string DisplayTitle, string TypeCode,
    string Status, bool IsSensitive, Guid? OrganizationUnitId, DateTime IndexedOn, DateTime CreatedOn, double Rank);
public sealed record SearchResultPageDto(IReadOnlyList<SearchResultItemDto> Items, long TotalCount, int Limit, int Offset);
public sealed record SearchIndexHealthDto(IReadOnlyList<SearchSourceHealthDto> Sources, DateTime GeneratedOn);
public sealed record SearchSourceHealthDto(string SourceType, long DocumentCount, DateTime? LastEventOccurredOn, long IndexedCount);
public sealed record ReindexCommand(Guid ActorId, string? SourceType = null) : IRequest;
public sealed record HealthQuery(Guid ActorId) : IRequest<SearchIndexHealthDto>;

/// <summary>
/// Candidate filter passed to the projection store. Scoping is deliberately
/// coarse: the repository returns matching rows with their organization
/// scopes and the handler authorizes each row (ADR-026); the store never
/// decides visibility.
/// </summary>
public sealed record SearchFilters(string Query, IReadOnlyList<string> SourceTypes, Guid? OrganizationUnitId,
    IReadOnlyList<string>? Statuses, bool IncludeSensitive);

/// <summary>
/// One projected search hit as read from the store, before authorization.
/// Carries the scopes needed to build per-row authorization contexts.
/// </summary>
public sealed record SearchProjectionRow(Guid Id, string SourceType, Guid SourceId, string DisplayTitle,
    string TypeCode, string Status, bool IsSensitive, Guid? OrganizationUnitId, IReadOnlyList<Guid> AdditionalScopes,
    DateTime IndexedOn, DateTime CreatedOn, double Rank);

public interface ISearchProjectionRepository
{
    /// <summary>
    /// Returns at most <paramref name="maxRows"/> candidate rows matching the
    /// filters, ordered by rank (desc), then indexed-on (desc), then id — the
    /// deterministic order the handler walks when filtering authorized rows.
    /// </summary>
    Task<IReadOnlyList<SearchProjectionRow>> FetchCandidatesAsync(SearchFilters filters, int offset, int maxRows, CancellationToken ct);
    Task<SearchIndexHealthDto> GetHealthAsync(CancellationToken ct);
}
public interface IProjectionReconciler
{
    Task ReconcileAsync(string? sourceType, CancellationToken ct);
}

public sealed class SearchQueryHandler(
    ISearchProjectionRepository repository,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator,
    IOptions<SearchOptions> options)
    : IRequestHandler<SearchQuery, SearchResultPageDto>
{
    private const int CandidateBatchSize = 200;
    private const int DefaultPageSize = 25;

    public async Task<SearchResultPageDto> Handle(SearchQuery query, CancellationToken cancellationToken)
    {
        if (query.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");

        var limit = Math.Min(query.Limit ?? DefaultPageSize, options.Value.MaxResultsPerPage);
        var offset = Math.Max(0, query.Offset);

        // Coarse gate first: the caller must hold the read capability at the
        // requested scope, or somewhere (global-context check) when no scope
        // was supplied. Row-level visibility is decided per row below; this
        // gate only rejects callers with no grant at all (ADR-026).
        var context = query.OrganizationUnitId is { } unit
            ? new AuthorizationContext(unit, SearchPermissions.ResourceType)
            : new AuthorizationContext(ResourceType: SearchPermissions.ResourceType);
        await guard.RequireAsync(query.ActorId, SearchPermissions.ResultRead, context, cancellationToken);
        if (query.Sensitive)
            await guard.RequireAsync(query.ActorId, SearchPermissions.ResultReadSensitive, context, cancellationToken);

        var filters = new SearchFilters(query.Query.Trim(),
            query.SourceType is { } single ? [single] : ResolveTypes(query.Types),
            query.OrganizationUnitId, query.Statuses, query.Sensitive);

        // Fail-closed read filtering at the query boundary: only rows the
        // caller may read are returned; nothing reveals the existence or count
        // of unauthorized hits (no enumeration oracle). A row is visible when
        // the caller holds the read permission at ANY of its scopes (primary,
        // additional, or the global resource scope when unscoped), and a
        // sensitive row additionally requires the sensitive capability there.
        var authorized = new List<SearchProjectionRow>();
        var cursor = 0;
        while (true)
        {
            var candidates = await repository.FetchCandidatesAsync(filters, cursor, CandidateBatchSize, cancellationToken);
            if (candidates.Count == 0) break;

            authorized.AddRange(await FilterAuthorizedAsync(query.ActorId, candidates, cancellationToken));

            if (candidates.Count < CandidateBatchSize) break;
            cursor += candidates.Count;
        }

        var items = authorized
            .Skip(offset)
            .Take(limit)
            .Select(ToDto)
            .ToList();

        return new(items, authorized.Count, limit, offset);
    }

    private async Task<List<SearchProjectionRow>> FilterAuthorizedAsync(Guid actorId, IReadOnlyList<SearchProjectionRow> candidates, CancellationToken ct)
    {
        var requests = new List<AuthorizationRequest>();
        var slices = new List<(SearchProjectionRow Row, int Count)>();
        foreach (var row in candidates)
        {
            var contexts = SearchAuthorization.ContextsFor(row);
            foreach (var context in contexts)
                requests.Add(new AuthorizationRequest(actorId, SearchPermissions.ResultRead, context));
            slices.Add((row, contexts.Count));
        }

        var decisions = await evaluator.EvaluateBatchAsync(requests, ct);

        var allowed = new List<SearchProjectionRow>();
        var index = 0;
        foreach (var (row, count) in slices)
        {
            var slice = decisions.Skip(index).Take(count).ToList();
            index += count;

            if (!slice.Any(d => d.Allowed))
                continue;

            if (row.IsSensitive)
            {
                var sensitiveRequests = SearchAuthorization.ContextsFor(row)
                    .Select(scope => new AuthorizationRequest(actorId, SearchPermissions.ResultReadSensitive, scope))
                    .ToList();
                var sensitiveDecisions = await evaluator.EvaluateBatchAsync(sensitiveRequests, ct);
                if (!sensitiveDecisions.Any(d => d.Allowed))
                    continue;
            }

            allowed.Add(row);
        }

        return allowed;
    }

    private static SearchResultItemDto ToDto(SearchProjectionRow row) =>
        new(row.Id, row.SourceType, row.SourceId, row.DisplayTitle, row.TypeCode, row.Status,
            row.IsSensitive, row.OrganizationUnitId, row.IndexedOn, row.CreatedOn, row.Rank);

    private static string[] ResolveTypes(IReadOnlyList<string>? types)
    {
        if (types is null || types.Count == 0) return [.. SearchSourceTypes.All];
        return types.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}

public sealed class ReindexCommandHandler(
    AuthorizationGuard guard,
    IProjectionReconciler reconciler,
    ILogger<ReindexCommandHandler> logger)
    : IRequestHandler<ReindexCommand>
{
    public async Task Handle(ReindexCommand request, CancellationToken cancellationToken)
    {
        if (request.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(request.ActorId, SearchPermissions.IndexManage, ct: cancellationToken);
        await reconciler.ReconcileAsync(request.SourceType, cancellationToken);
        logger.ReindexCompleted(request.SourceType ?? "all");
    }
}

public sealed class HealthQueryHandler(AuthorizationGuard guard, ISearchProjectionRepository repository)
    : IRequestHandler<HealthQuery, SearchIndexHealthDto>
{
    public async Task<SearchIndexHealthDto> Handle(HealthQuery request, CancellationToken cancellationToken)
    {
        if (request.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(request.ActorId, SearchPermissions.IndexManage, ct: cancellationToken);
        return await repository.GetHealthAsync(cancellationToken);
    }
}

internal static partial class SearchApplicationLog
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Search index reconciliation completed for {SourceType}.")]
    public static partial void ReindexCompleted(this ILogger logger, string sourceType);
}
