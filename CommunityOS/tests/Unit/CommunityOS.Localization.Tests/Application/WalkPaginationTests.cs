using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Localization.Application;
using CommunityOS.Localization.Domain;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;
using AuthorizationDecision = CommunityOS.Authorization.Application.Authorization.AuthorizationDecision;

namespace CommunityOS.Localization.Tests.Application;

/// <summary>
/// Cursor-pagination proofs for the entries walk (ADR-029 decision 13; 16G
/// findings LOC-02/LOC-03): the handler emits the opaque continuation cursor
/// exactly when more rows exist, following cursors yields every matching row
/// exactly once in ratified order, and filtered pages are complete — the page
/// window can no longer swallow matches beyond it.
/// </summary>
public sealed class WalkPaginationTests
{
    private const string DefaultNamespace = "ui";

    /// <summary>Faithful in-memory emulation of the store's keyset walk:
    /// ordering (namespace, key, id), state/search/cursor filters and the
    /// row cap mirror LocalizationReader's contract.</summary>
    private sealed class InMemoryWalkReader : ILocalizationReader
    {
        private sealed class Row(
            Guid id, string ns, string key, bool isDeprecated, HashSet<ReviewState> states)
        {
            public Guid Id { get; } = id;
            public string Ns { get; } = ns;
            public string Key { get; } = key;
            public bool IsDeprecated { get; } = isDeprecated;
            public HashSet<ReviewState> States { get; } = states;
        }

        private readonly List<Row> _rows;

        public InMemoryWalkReader(params (string Key, bool Deprecated, HashSet<ReviewState> States)[] seed)
        {
            _rows = seed
                .Select(s => new Row(Guid.NewGuid(), DefaultNamespace, s.Key, s.Deprecated, s.States))
                .OrderBy(r => r.Ns, StringComparer.Ordinal)
                .ThenBy(r => r.Key, StringComparer.Ordinal)
                .ThenBy(r => r.Id)
                .ToList();
        }

        public Task<IReadOnlyList<EntryRow>> WalkEntriesAsync(
            Guid? namespaceId, string? stateFilter, string? search,
            (string NamespaceName, string Key, Guid Id)? afterKey, int maxRows, CancellationToken ct)
        {
            IEnumerable<Row> query = _rows;

            if (!string.IsNullOrWhiteSpace(stateFilter))
            {
                query = stateFilter == "deprecated"
                    ? query.Where(r => r.IsDeprecated)
                    : query.Where(r => r.States.Contains(Enum.Parse<ReviewState>(
                        stateFilter.Replace("-", string.Empty).Replace("_", string.Empty),
                        true)));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(r => r.Key.Contains(search, StringComparison.Ordinal));
            }

            if (afterKey.HasValue)
            {
                var (ns, key, id) = afterKey.Value;
                query = query.Where(r =>
                    string.Compare(r.Ns, ns, StringComparison.Ordinal) > 0 ||
                    (r.Ns == ns && string.Compare(r.Key, key, StringComparison.Ordinal) > 0) ||
                    (r.Ns == ns && r.Key == key && r.Id.CompareTo(id) > 0));
            }

            var page = query
                .OrderBy(r => r.Ns, StringComparer.Ordinal)
                .ThenBy(r => r.Key, StringComparer.Ordinal)
                .ThenBy(r => r.Id)
                .Take(maxRows)
                .Select(r => new EntryRow(
                    r.Id, Guid.NewGuid(), r.Ns, r.Key,
                    r.IsDeprecated, r.IsDeprecated ? DateTime.UtcNow : null,
                    []))
                .ToList();

            return Task.FromResult<IReadOnlyList<EntryRow>>(page);
        }

        // Unused by these tests.
        public Task<IReadOnlyList<Locale>> ListLocalesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Locale>>([]);
        public Task<Locale?> FindLocaleAsync(string code, CancellationToken ct) =>
            Task.FromResult<Locale?>(null);
        public Task<Locale?> FindDefaultLocaleAsync(CancellationToken ct) =>
            Task.FromResult<Locale?>(null);
        public Task<bool> LocaleCodeExistsAsync(string code, CancellationToken ct) => Task.FromResult(false);
        public Task<ResourceNamespace?> FindNamespaceAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<ResourceNamespace?>(null);
        public Task<ResourceNamespace?> FindNamespaceByNameAsync(string name, CancellationToken ct) =>
            Task.FromResult<ResourceNamespace?>(null);
        public Task<IReadOnlyList<NamespaceRow>> ListNamespacesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<NamespaceRow>>([]);
        public Task<ResourceEntry?> FindTrackedEntryAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<ResourceEntry?>(null);
        public Task<ResourceEntry?> FindTrackedEntryByKeyAsync(Guid namespaceId, string key, CancellationToken ct) =>
            Task.FromResult<ResourceEntry?>(null);
        public Task<string?> FindNamespaceNameAsync(Guid namespaceId, CancellationToken ct) =>
            Task.FromResult<string?>(null);
        public Task<IReadOnlyList<ExportCandidateRow>> LoadExportCandidatesAsync(
            Guid? namespaceId, IReadOnlyList<string> cultures, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ExportCandidateRow>>([]);
        public Task<EntityTranslation?> FindTrackedEntityTranslationAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<EntityTranslation?>(null);
        public Task<EntityTranslation?> FindEntityTranslationAsync(
            string sourceContext, string entityType, Guid entityId, string field,
            string culture, CancellationToken ct) =>
            Task.FromResult<EntityTranslation?>(null);
        public Task<IReadOnlyList<EntityTranslationRow>> QueryEntityTranslationsAsync(
            IReadOnlyList<EntityTranslationRef> refs, bool includePending, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EntityTranslationRow>>([]);
        public Task<TranslationSuggestion?> FindSuggestionAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<TranslationSuggestion?>(null);
        public Task<IReadOnlyList<SuggestionRow>> ListSuggestionsAsync(
            string? statusFilter, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SuggestionRow>>([]);
        public Task<long> GetCatalogVersionAsync(CancellationToken ct) => Task.FromResult(0L);
    }

    private static WalkEntriesHandler Handler(ILocalizationReader reader) =>
        new(reader, new AuthorizationGuard(new AllowAllEvaluator()),
            Options.Create(new LocalizationOptions()));

    private static async Task<List<EntryRow>> FollowAllPagesAsync(
        ILocalizationReader reader, string? state, int limit)
    {
        var all = new List<EntryRow>();
        string? cursor = null;
        var hops = 0;

        while (hops++ < 100)
        {
            var page = await Handler(reader).Handle(
                new WalkEntriesQuery(Guid.NewGuid(), null, state, null, cursor, limit),
                CancellationToken.None);
            all.AddRange(page.Items);

            if (page.NextCursor is null)
            {
                return all;
            }

            cursor = page.NextCursor;
        }

        throw new InvalidOperationException("Pagination did not terminate.");
    }

    [Fact]
    public async Task First_page_returns_cursor_when_more_data_exists()
    {
        var reader = new InMemoryWalkReader(
            ("k01", false, ReadOnly(ReviewState.Approved)),
            ("k02", false, ReadOnly(ReviewState.Approved)),
            ("k03", false, ReadOnly(ReviewState.Approved)));

        var page = await Handler(reader).Handle(
            new WalkEntriesQuery(Guid.NewGuid(), null, null, null, null, 2),
            CancellationToken.None);

        page.Items.Should().HaveCount(2);
        page.NextCursor.Should().NotBeNull();
    }

    [Fact]
    public async Task Final_page_returns_no_cursor()
    {
        var reader = new InMemoryWalkReader(
            ("k01", false, ReadOnly(ReviewState.Approved)),
            ("k02", false, ReadOnly(ReviewState.Approved)));

        var first = await Handler(reader).Handle(
            new WalkEntriesQuery(Guid.NewGuid(), null, null, null, null, 2),
            CancellationToken.None);

        first.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Following_cursors_yields_every_row_exactly_once_in_order()
    {
        var keys = Enumerable.Range(1, 7).Select(i => $"k{i:00}").ToArray();
        var reader = new InMemoryWalkReader(
            keys.Select(k => (k, false, ReadOnly(ReviewState.Approved))).ToArray());

        var walked = await FollowAllPagesAsync(reader, null, 3);

        walked.Select(r => r.Key).Should().Equal(keys);
    }

    [Fact]
    public async Task Filtered_walk_pages_are_complete_disjoint_and_ordered()
    {
        // Interleave approved/draft rows so pre-remediation take-before-filter
        // pagination would silently drop approved rows behind draft windows.
        var reader = new InMemoryWalkReader(
            ("a-approved-1", false, ReadOnly(ReviewState.Approved)),
            ("b-draft-1", false, ReadOnly(ReviewState.Draft)),
            ("c-approved-2", false, ReadOnly(ReviewState.Approved)),
            ("d-draft-2", false, ReadOnly(ReviewState.Draft)),
            ("e-approved-3", false, ReadOnly(ReviewState.Approved)),
            ("f-deprecated", true, ReadOnly(ReviewState.Approved)));

        var walked = await FollowAllPagesAsync(reader, "approved", 2);

        // Deprecation is retention-only: the retained approved revision keeps
        // the entry in the approved state view (decision 9).
        walked.Select(r => r.Key).Should().Equal(
            "a-approved-1", "c-approved-2", "e-approved-3", "f-deprecated");
    }

    [Fact]
    public async Task Snake_case_state_tokens_filter_correctly()
    {
        var reader = new InMemoryWalkReader(
            ("a-draft", false, ReadOnly(ReviewState.Draft)),
            ("b-inreview", false, ReadOnly(ReviewState.InReview)),
            ("c-approved", false, ReadOnly(ReviewState.Approved)),
            ("d-inreview", false, ReadOnly(ReviewState.InReview)));

        var walked = await FollowAllPagesAsync(reader, "in_review", 1);

        walked.Select(r => r.Key).Should().Equal("b-inreview", "d-inreview");
    }

    [Fact]
    public async Task Zero_limit_falls_back_to_the_default_page_size()
    {
        var reader = new InMemoryWalkReader(
            Enumerable.Range(1, 30)
                .Select(i => ($"k{i:00}", false, ReadOnly(ReviewState.Approved)))
                .ToArray());

        var page = await Handler(reader).Handle(
            new WalkEntriesQuery(Guid.NewGuid(), null, null, null, null, 0),
            CancellationToken.None);

        page.Items.Should().HaveCount(25);
        page.NextCursor.Should().NotBeNull();
    }

    [Fact]
    public async Task Oversized_limit_is_clamped_to_max_page_size()
    {
        var reader = new InMemoryWalkReader(
            Enumerable.Range(1, 200)
                .Select(i => ($"k{i:000}", false, ReadOnly(ReviewState.Approved)))
                .ToArray());

        var page = await Handler(reader).Handle(
            new WalkEntriesQuery(Guid.NewGuid(), null, null, null, null, 5000),
            CancellationToken.None);

        page.Items.Should().HaveCount(100);
    }

    private static HashSet<ReviewState> ReadOnly(params ReviewState[] states) =>
        [.. states];

    private sealed class AllowAllEvaluator : IAuthorizationEvaluator
    {
        public Task<AuthorizationDecision> EvaluateAsync(AuthorizationRequest request, CancellationToken ct = default) =>
            Task.FromResult(AuthorizationDecision.Allow(
                Guid.NewGuid().ToString("N"), [Guid.NewGuid().ToString("N")], DateTime.UtcNow));

        public Task<IReadOnlyList<AuthorizationDecision>> EvaluateBatchAsync(
            IReadOnlyList<AuthorizationRequest> requests, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AuthorizationDecision>>(
                requests.Select(r => AuthorizationDecision.Allow(
                    Guid.NewGuid().ToString("N"), [Guid.NewGuid().ToString("N")], DateTime.UtcNow)).ToList());
    }
}
