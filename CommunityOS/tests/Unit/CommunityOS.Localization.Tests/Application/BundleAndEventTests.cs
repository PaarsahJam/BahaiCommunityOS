using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Localization.Application;
using CommunityOS.Contracts.Localization;
using CommunityOS.Localization.Domain;
using CommunityOS.Localization.Domain.Events;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Reflection;
using Xunit;
using AuthorizationDecision = CommunityOS.Authorization.Application.Authorization.AuthorizationDecision;

namespace CommunityOS.Localization.Tests.Application;

public sealed class BundleResolverTests
{
    private static ExportCandidateRow Candidate(
        string ns, string key, params (string Culture, string Value)[] values)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (culture, value) in values)
        {
            dict[culture] = value;
        }

        return new ExportCandidateRow(Guid.NewGuid(), ns, key, dict);
    }

    [Fact]
    public void Fallback_walks_region_to_language_then_default()
    {
        var candidates = new List<ExportCandidateRow>
        {
            Candidate("ui", "greeting", ("fa", "سلام")),
            Candidate("ui", "farewell", ("en", "Goodbye"))
        };

        var items = BundleResolver.Resolve(candidates, "fa-IR", "en");

        items.Should().HaveCount(2);
        items[0].Value.Should().Be("سلام");
        items[0].ResolvedFrom.Should().Be("fa");
        items[1].Value.Should().Be("Goodbye");
        items[1].ResolvedFrom.Should().Be("en");
    }

    [Fact]
    public void Unresolved_keys_are_omitted_fail_visible()
    {
        var candidates = new List<ExportCandidateRow>
        {
            Candidate("ui", "only-de", ("de", "Hallo"))
        };

        var items = BundleResolver.Resolve(candidates, "fa", "en");

        items.Should().BeEmpty();
    }

    [Fact]
    public void Requested_culture_beats_fallbacks()
    {
        var candidates = new List<ExportCandidateRow>
        {
            Candidate("ui", "greeting",
                ("fa-ir", "درود"), ("fa", "سلام"), ("en", "Hello"))
        };

        var items = BundleResolver.Resolve(candidates, "fa-IR", "en");

        items.Single().Value.Should().Be("درود");
        items.Single().ResolvedFrom.Should().Be("fa-ir");
    }

    [Fact]
    public void Region_tag_wins_over_language_and_language_over_default()
    {
        var candidates = new List<ExportCandidateRow>
        {
            Candidate("ui", "greeting", ("fa", "سلام"), ("en", "Hello"))
        };

        var items = BundleResolver.Resolve(candidates, "fa-IR", "en");

        items.Single().Value.Should().Be("سلام");
        items.Single().ResolvedFrom.Should().Be("fa");
    }

    [Fact]
    public void En_only_key_resolves_inside_an_fa_ir_bundle()
    {
        var candidates = new List<ExportCandidateRow>
        {
            Candidate("ui", "greeting", ("en", "Hello"))
        };

        var items = BundleResolver.Resolve(candidates, "fa-IR", "en");

        items.Single().Value.Should().Be("Hello");
        items.Single().ResolvedFrom.Should().Be("en");
    }

    [Fact]
    public void Chain_collapses_duplicate_cultures_exactly_once()
    {
        // The requested language already IS the default; the chain must not
        // contain it twice.
        var chain = BundleResolver.BuildChain("fa", "fa");

        chain.Should().Equal("fa");
    }

    [Fact]
    public void Chain_is_requested_parents_then_default()
    {
        var chain = BundleResolver.BuildChain("fa-IR", "en");

        chain.Should().Equal("fa-ir", "fa", "en");
    }

    [Fact]
    public void Etags_are_deterministic_per_version()
    {
        BundleResolver.ETagFor(7).Should().Be(BundleResolver.ETagFor(7));
        BundleResolver.ETagFor(7).Should().NotBe(BundleResolver.ETagFor(8));
    }
}

/// <summary>
/// Handler-level export proofs (LOC-01 remediation): the bundle handler must
/// hand the reader the COMPLETE deduplicated fallback chain — including the
/// default locale as the terminal link — and must forward the configured
/// export cap unchanged.
/// </summary>
public sealed class ExportBundleHandlerTests
{
    private sealed class RecordingReader : ILocalizationReader
    {
        public IReadOnlyList<string>? RequestedCultures { get; private set; }
        public int RequestedMaxRows { get; private set; }

        public Task<IReadOnlyList<ExportCandidateRow>> LoadExportCandidatesAsync(
            Guid? namespaceId, IReadOnlyList<string> cultures, int maxRows, CancellationToken ct)
        {
            RequestedCultures = cultures;
            RequestedMaxRows = maxRows;
            return Task.FromResult<IReadOnlyList<ExportCandidateRow>>([]);
        }

        public Task<long> GetCatalogVersionAsync(CancellationToken ct) => Task.FromResult(1L);

        public Task<Locale?> FindDefaultLocaleAsync(CancellationToken ct) =>
            Task.FromResult<Locale?>(Locale.CreateDefaultSeed(
                Guid.NewGuid(), "en", "English", Guid.NewGuid(), DateTime.UtcNow));

        public Task<IReadOnlyList<EntryRow>> WalkEntriesAsync(
            Guid? namespaceId, string? stateFilter, string? search,
            (string NamespaceName, string Key, Guid Id)? afterKey, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EntryRow>>([]);

        public Task<IReadOnlyList<Locale>> ListLocalesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Locale>>([]);

        public Task<Locale?> FindLocaleAsync(string code, CancellationToken ct) =>
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
    }

    [Fact]
    public async Task Handler_requests_the_entire_deduplicated_fallback_chain()
    {
        var reader = new RecordingReader();
        var guard = new AuthorizationGuard(new AllowAllEvaluator());

        await new ExportBundleHandler(reader, guard, Options.Create(new LocalizationOptions()))
            .Handle(new ExportBundleQuery(Guid.NewGuid(), null, "fa-IR"), CancellationToken.None);

        reader.RequestedCultures.Should().Equal("fa-ir", "fa", "en");
    }

    [Fact]
    public async Task Handler_requests_cultures_once_when_language_equals_default()
    {
        var reader = new RecordingReader();
        var guard = new AuthorizationGuard(new AllowAllEvaluator());

        await new ExportBundleHandler(reader, guard, Options.Create(new LocalizationOptions()))
            .Handle(new ExportBundleQuery(Guid.NewGuid(), null, "en"), CancellationToken.None);

        reader.RequestedCultures.Should().Equal("en");
    }

    [Fact]
    public async Task Handler_forwards_the_configured_export_cap()
    {
        var reader = new RecordingReader();
        var guard = new AuthorizationGuard(new AllowAllEvaluator());
        var options = new LocalizationOptions { MaxExportKeys = 4242 };

        await new ExportBundleHandler(reader, guard, Options.Create(options))
            .Handle(new ExportBundleQuery(Guid.NewGuid(), null, "en"), CancellationToken.None);

        reader.RequestedMaxRows.Should().Be(4242);
    }

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

/// <summary>
/// Payload allowlist (mirrors the Correspondence gate): the ratified catalog
/// fact carries codes, a version and a timestamp only — never translation
/// values or any other catalog content.
/// </summary>
public sealed class CatalogEventPayloadAllowlistTests
{
    [Fact]
    public void Integration_contract_properties_are_exactly_the_allowlist()
    {
        var properties = typeof(LocalizationCatalogChanged)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        properties.Should().Equal([
            "BundleVersion", "CatalogContext", "Culture", "Namespace", "OccurredOn"
        ]);
    }

    [Fact]
    public void Domain_event_carries_no_catalog_content()
    {
        var domainProperties = typeof(CatalogChangedDomainEvent)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        domainProperties.Should().Equal(["BundleVersion", "CatalogContext", "Culture", "Namespace"]);
    }
}
