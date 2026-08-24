using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Localization.Application;
using CommunityOS.Localization.Application.Permissions;
using CommunityOS.Localization.Domain;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;
using AuthorizationDecision = CommunityOS.Authorization.Application.Authorization.AuthorizationDecision;

namespace CommunityOS.Localization.Tests.Application;

/// <summary>
/// The ratified permission contract (ADR-029 decision 11), enforced at the
/// handler boundary: exactly five capabilities, exact ordinal strings, one
/// check per operation — administration never implies read and review never
/// implies propose.
/// </summary>
public sealed class LocalizationAuthorizationTests
{
    private static readonly Guid Actor = Guid.NewGuid();

    private sealed class RecordingEvaluator : IAuthorizationEvaluator
    {
        public List<string> Requested { get; } = [];

        public Task<AuthorizationDecision> EvaluateAsync(AuthorizationRequest request, CancellationToken ct = default)
        {
            Requested.Add(request.Permission);
            return Task.FromResult(AuthorizationDecision.Allow(
                Guid.NewGuid().ToString("N"), [Guid.NewGuid().ToString("N")], DateTime.UtcNow));
        }

        public Task<IReadOnlyList<AuthorizationDecision>> EvaluateBatchAsync(
            IReadOnlyList<AuthorizationRequest> requests, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubReader : ILocalizationReader
    {
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

        public Task<IReadOnlyList<EntryRow>> WalkEntriesAsync(
            Guid? namespaceId, string? stateFilter, string? search,
            (string NamespaceName, string Key, Guid Id)? afterKey, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EntryRow>>([]);

        public Task<IReadOnlyList<ExportCandidateRow>> LoadExportCandidatesAsync(
            Guid? namespaceId, IReadOnlyList<string> cultures, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ExportCandidateRow>>([]);

        public Task<EntityTranslation?> FindTrackedEntityTranslationAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<EntityTranslation?>(null);

        public Task<EntityTranslation?> FindEntityTranslationAsync(
            string sourceContext, string entityType, Guid entityId, string field, string culture, CancellationToken ct) =>
            Task.FromResult<EntityTranslation?>(null);

        public Task<IReadOnlyList<EntityTranslationRow>> QueryEntityTranslationsAsync(
            IReadOnlyList<EntityTranslationRef> refs, bool includePending, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EntityTranslationRow>>([]);

        public Task<TranslationSuggestion?> FindSuggestionAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<TranslationSuggestion?>(null);

        public Task<IReadOnlyList<SuggestionRow>> ListSuggestionsAsync(string? statusFilter, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SuggestionRow>>([]);

        public Task<long> GetCatalogVersionAsync(CancellationToken ct) => Task.FromResult(0L);
    }

    private sealed class StubJournal : ILocalizationJournal
    {
        public Task SaveLocaleAsync(Locale locale, Locale? previousDefault, CancellationToken ct) => Task.CompletedTask;

        public Task SaveNamespaceAsync(ResourceNamespace @namespace, CancellationToken ct) => Task.CompletedTask;

        public Task SaveEntryAsync(ResourceEntry entry, CancellationToken ct) => Task.CompletedTask;

        public Task SaveEntityTranslationAsync(EntityTranslation translation, CancellationToken ct) => Task.CompletedTask;

        public Task SaveSuggestionAsync(TranslationSuggestion suggestion, CancellationToken ct) => Task.CompletedTask;

        public Task PublishCatalogChangeAsync(
            ResourceEntry entry, Func<long, CancellationToken, Task> publishEvent, CancellationToken ct) =>
            publishEvent(1, ct);

        public Task PublishCatalogChangeAsync(
            EntityTranslation translation, Func<long, CancellationToken, Task> publishEvent, CancellationToken ct) =>
            publishEvent(1, ct);

        public Task SaveSuggestionDecisionAsync(
            TranslationSuggestion suggestion,
            ResourceEntry? entryTarget,
            EntityTranslation? translationTarget,
            CancellationToken ct) => Task.CompletedTask;

        public Task FlushOutboxAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private static (AuthorizationGuard Guard, RecordingEvaluator Evaluator) Guard() 
    {
        var evaluator = new RecordingEvaluator();
        return (new AuthorizationGuard(evaluator), evaluator);
    }

    [Fact]
    public async Task Every_locale_read_requires_exactly_locale_read()
    {
        var (guard, evaluator) = Guard();
        await new ListLocalesHandler(new StubReader(), guard)
            .Handle(new ListLocales(Actor), CancellationToken.None);
        evaluator.Requested.Should().Equal([LocalizationPermissions.LocaleRead]);
    }

    [Fact]
    public async Task Locale_lifecycle_commands_require_manage_only()
    {
        var reader = new StubReader();
        var journal = new StubJournal();

        var (createGuard, createEval) = Guard();
        await new CreateLocaleHandler(reader, journal, createGuard)
            .Handle(new CreateLocaleCommand(Actor, "fr", null), CancellationToken.None);
        createEval.Requested.Should().Equal([LocalizationPermissions.LocaleManage]);

        var locale = Locale.Register("fr", null, Actor, DateTime.UtcNow);
        var trackingReader = new SingleLocaleReader(locale);

        var (activateGuard, activateEval) = Guard();
        await new ActivateLocaleHandler(trackingReader, journal, activateGuard)
            .Handle(new ActivateLocaleCommand(Actor, "fr"), CancellationToken.None);
        activateEval.Requested.Should().Equal([LocalizationPermissions.LocaleManage]);
    }

    [Fact]
    public async Task Resource_walk_and_export_require_resource_read()
    {
        var (walkGuard, walkEval) = Guard();
        await new WalkEntriesHandler(new StubReader(), walkGuard)
            .Handle(new WalkEntriesQuery(Actor, null, null, null, null, 10), CancellationToken.None);
        walkEval.Requested.Should().Equal([LocalizationPermissions.ResourceRead]);

        var (exportGuard, exportEval) = Guard();
        await new ExportBundleHandler(new StubReader(), exportGuard,
                Options.Create(new LocalizationOptions()))
            .Handle(new ExportBundleQuery(Actor, null, "en"), CancellationToken.None);
        exportEval.Requested.Should().Equal([LocalizationPermissions.ResourceRead]);
    }

    [Fact]
    public async Task Authoring_requires_propose_and_never_review()
    {
        var ns = ResourceNamespace.Create("greeting", null, Actor, DateTime.UtcNow);
        var reader = new SeedReader(ns);
        var (guard, evaluator) = Guard();
        var handler = new CreateResourceEntryHandler(reader, new StubJournal(), guard);

        await handler.Handle(
            new CreateResourceEntryCommand(Actor, ns.Id, "hello", "en", "Hello"),
            CancellationToken.None);

        evaluator.Requested.Should().Equal([LocalizationPermissions.ResourcePropose]);
    }

    [Fact]
    public async Task Approval_requires_review_and_never_propose()
    {
        var actor = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var ns = ResourceNamespace.Create("greeting", null, actor, now);
        var entry = ResourceEntry.Create(ns.Id, "hello", now);
        entry.ProposeDraft("en", "Hello", ContentProvenance.ForHuman(), actor, now);
        entry.SubmitForReview(entry.Revisions[0].Id, now);

        var reader = new SeedReader(ns, entry);
        var (guard, evaluator) = Guard();
        var publisher = new RecordingPublisher();

        await new ApproveRevisionHandler(reader, new StubJournal(), guard, publisher)
            .Handle(new ApproveRevisionCommand(actor, entry.Id, entry.Revisions[0].Id),
                CancellationToken.None);

        evaluator.Requested.Should().Equal([LocalizationPermissions.ResourceReview]);
        publisher.Published.Should().HaveCount(1);
    }

    [Fact]
    public async Task Empty_actor_is_rejected_before_any_evaluation()
    {
        var (guard, evaluator) = Guard();
        var handler = new ListLocalesHandler(new StubReader(), guard);

        var act = async () => await handler.Handle(
            new ListLocales(Guid.Empty), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        evaluator.Requested.Should().BeEmpty();
    }

    private sealed class SingleLocaleReader : ILocalizationReader
    {
        private readonly Locale _locale;

        public SingleLocaleReader(Locale locale) => _locale = locale;

        public Task<IReadOnlyList<Locale>> ListLocalesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Locale>>([_locale]);

        public Task<Locale?> FindLocaleAsync(string code, CancellationToken ct) =>
            Task.FromResult<Locale?>(_locale.Code == code ? _locale : null);

        public Task<Locale?> FindDefaultLocaleAsync(CancellationToken ct) => Task.FromResult<Locale?>(null);

        public Task<bool> LocaleCodeExistsAsync(string code, CancellationToken ct) =>
            Task.FromResult(_locale.Code == code);

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

        public Task<IReadOnlyList<EntryRow>> WalkEntriesAsync(
            Guid? namespaceId, string? stateFilter, string? search,
            (string NamespaceName, string Key, Guid Id)? afterKey, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EntryRow>>([]);

        public Task<IReadOnlyList<ExportCandidateRow>> LoadExportCandidatesAsync(
            Guid? namespaceId, IReadOnlyList<string> cultures, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ExportCandidateRow>>([]);

        public Task<EntityTranslation?> FindTrackedEntityTranslationAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<EntityTranslation?>(null);

        public Task<EntityTranslation?> FindEntityTranslationAsync(
            string sourceContext, string entityType, Guid entityId, string field, string culture, CancellationToken ct) =>
            Task.FromResult<EntityTranslation?>(null);

        public Task<IReadOnlyList<EntityTranslationRow>> QueryEntityTranslationsAsync(
            IReadOnlyList<EntityTranslationRef> refs, bool includePending, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EntityTranslationRow>>([]);

        public Task<TranslationSuggestion?> FindSuggestionAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<TranslationSuggestion?>(null);

        public Task<IReadOnlyList<SuggestionRow>> ListSuggestionsAsync(string? statusFilter, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SuggestionRow>>([]);

        public Task<long> GetCatalogVersionAsync(CancellationToken ct) => Task.FromResult(0L);
    }

    private sealed class SeedReader : ILocalizationReader
    {
        private readonly ResourceNamespace? _namespace;
        private readonly ResourceEntry? _entry;

        public SeedReader(ResourceNamespace? @namespace = null, ResourceEntry? entry = null)
        {
            _namespace = @namespace;
            _entry = entry;
        }

        public Task<IReadOnlyList<Locale>> ListLocalesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Locale>>([
                Locale.CreateDefaultSeed(Guid.NewGuid(), "en", "English",
                    Guid.NewGuid(), DateTime.UtcNow)
            ]);

        public Task<Locale?> FindLocaleAsync(string code, CancellationToken ct) =>
            Task.FromResult<Locale?>(code == "en"
                ? Locale.CreateDefaultSeed(Guid.NewGuid(), "en", "English", Guid.NewGuid(), DateTime.UtcNow)
                : null);

        public Task<Locale?> FindDefaultLocaleAsync(CancellationToken ct) => Task.FromResult<Locale?>(null);

        public Task<bool> LocaleCodeExistsAsync(string code, CancellationToken ct) => Task.FromResult(false);

        public Task<ResourceNamespace?> FindNamespaceAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_namespace?.Id == id ? _namespace : null);

        public Task<ResourceNamespace?> FindNamespaceByNameAsync(string name, CancellationToken ct) =>
            Task.FromResult<ResourceNamespace?>(null);

        public Task<IReadOnlyList<NamespaceRow>> ListNamespacesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<NamespaceRow>>([]);

        public Task<ResourceEntry?> FindTrackedEntryAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_entry?.Id == id ? _entry : null);

        public Task<ResourceEntry?> FindTrackedEntryByKeyAsync(Guid namespaceId, string key, CancellationToken ct) =>
            Task.FromResult<ResourceEntry?>(null);

        public Task<string?> FindNamespaceNameAsync(Guid namespaceId, CancellationToken ct) =>
            Task.FromResult(_namespace?.Id == namespaceId ? _namespace!.Name : null);

        public Task<IReadOnlyList<EntryRow>> WalkEntriesAsync(
            Guid? namespaceId, string? stateFilter, string? search,
            (string NamespaceName, string Key, Guid Id)? afterKey, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EntryRow>>([]);

        public Task<IReadOnlyList<ExportCandidateRow>> LoadExportCandidatesAsync(
            Guid? namespaceId, IReadOnlyList<string> cultures, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ExportCandidateRow>>([]);

        public Task<EntityTranslation?> FindTrackedEntityTranslationAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<EntityTranslation?>(null);

        public Task<EntityTranslation?> FindEntityTranslationAsync(
            string sourceContext, string entityType, Guid entityId, string field, string culture, CancellationToken ct) =>
            Task.FromResult<EntityTranslation?>(null);

        public Task<IReadOnlyList<EntityTranslationRow>> QueryEntityTranslationsAsync(
            IReadOnlyList<EntityTranslationRef> refs, bool includePending, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EntityTranslationRow>>([]);

        public Task<TranslationSuggestion?> FindSuggestionAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<TranslationSuggestion?>(null);

        public Task<IReadOnlyList<SuggestionRow>> ListSuggestionsAsync(string? statusFilter, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SuggestionRow>>([]);

        public Task<long> GetCatalogVersionAsync(CancellationToken ct) => Task.FromResult(0L);
    }

    private sealed class RecordingPublisher : MediatR.IPublisher
    {
        public List<object> Published { get; } = [];

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : MediatR.INotification
        {
            Published.Add(notification!);
            return Task.CompletedTask;
        }

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task Publish(object[] notifications, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
