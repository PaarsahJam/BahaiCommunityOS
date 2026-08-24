using CommunityOS.Localization.Domain;

namespace CommunityOS.Localization.Application;

/// <summary>
/// Read side: deterministic catalog queries and lookups. Implementations apply
/// filters, projections and ordering at the store; visibility is decided by
/// the handlers through the Authorization service (ADR-029 decision 11).
/// </summary>
public interface ILocalizationReader
{
    Task<IReadOnlyList<Locale>> ListLocalesAsync(CancellationToken ct);

    Task<Locale?> FindLocaleAsync(string code, CancellationToken ct);

    Task<Locale?> FindDefaultLocaleAsync(CancellationToken ct);

    Task<bool> LocaleCodeExistsAsync(string code, CancellationToken ct);

    Task<ResourceNamespace?> FindNamespaceAsync(Guid id, CancellationToken ct);

    Task<ResourceNamespace?> FindNamespaceByNameAsync(string name, CancellationToken ct);

    Task<IReadOnlyList<NamespaceRow>> ListNamespacesAsync(CancellationToken ct);

    Task<ResourceEntry?> FindTrackedEntryAsync(Guid id, CancellationToken ct);

    Task<ResourceEntry?> FindTrackedEntryByKeyAsync(Guid namespaceId, string key, CancellationToken ct);

    Task<string?> FindNamespaceNameAsync(Guid namespaceId, CancellationToken ct);

    /// <summary>Deterministic keyset walk over active entries ordered by
    /// (namespace name, key, id); <paramref name="afterKey"/> is the opaque
    /// cursor payload (namespace, key, id) of the last row of the previous
    /// page, or null for the first page.</summary>
    Task<IReadOnlyList<EntryRow>> WalkEntriesAsync(
        Guid? namespaceId, string? stateFilter, string? search,
        (string NamespaceName, string Key, Guid Id)? afterKey, int maxRows, CancellationToken ct);

    /// <summary>All non-deprecated entries that have an approved revision in
    /// any of the supplied cultures, with their approved values keyed by
    /// culture â€” the raw material for export-bundle fallback resolution.</summary>
    Task<IReadOnlyList<ExportCandidateRow>> LoadExportCandidatesAsync(
        Guid? namespaceId, IReadOnlyList<string> cultures, int maxRows, CancellationToken ct);

    Task<EntityTranslation?> FindTrackedEntityTranslationAsync(Guid id, CancellationToken ct);

    Task<EntityTranslation?> FindEntityTranslationAsync(
        string sourceContext, string entityType, Guid entityId, string field, string culture, CancellationToken ct);

    Task<IReadOnlyList<EntityTranslationRow>> QueryEntityTranslationsAsync(
        IReadOnlyList<EntityTranslationRef> refs, bool includePending, int maxRows, CancellationToken ct);

    Task<TranslationSuggestion?> FindSuggestionAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<SuggestionRow>> ListSuggestionsAsync(string? statusFilter, int maxRows, CancellationToken ct);

    /// <summary>The singleton monotonic catalog version used in bundle ETags
    /// and <c>LocalizationCatalogChanged</c> payloads.</summary>
    Task<long> GetCatalogVersionAsync(CancellationToken ct);
}

/// <summary>
/// Write side: aggregate persistence where every save is a single SaveChanges,
/// so outbox-captured integration events commit atomically with the catalog
/// change (ADR-015; ADR-029 decisions 14/16). The journal also owns the
/// singleton catalog-version counter, bumped inside the same transaction as
/// the publishing mutation.
/// </summary>
public interface ILocalizationJournal
{
    Task SaveLocaleAsync(Locale locale, Locale? previousDefault, CancellationToken ct);

    Task SaveNamespaceAsync(ResourceNamespace resourceNamespace, CancellationToken ct);

    Task SaveEntryAsync(ResourceEntry entry, CancellationToken ct);

    Task SaveEntityTranslationAsync(EntityTranslation translation, CancellationToken ct);

    Task SaveSuggestionAsync(TranslationSuggestion suggestion, CancellationToken ct);

    /// <summary>Publishing saves: bumps the singleton catalog version inside
    /// the save transaction, invokes <paramref name="publishEvent"/> with the
    /// NEW version so outbox rows are buffered, then performs ONE SaveChanges
    /// and commits â€” catalog mutation, version bump and integration event are
    /// atomic (ADR-029 decisions 14/16).</summary>
    Task PublishCatalogChangeAsync(
        ResourceEntry entry, Func<long, CancellationToken, Task> publishEvent, CancellationToken ct);

    Task PublishCatalogChangeAsync(
        EntityTranslation translation, Func<long, CancellationToken, Task> publishEvent, CancellationToken ct);

    /// <summary>A suggestion decision and the target revision it spawns must
    /// land in one save (accept-into-review composes both).</summary>
    Task SaveSuggestionDecisionAsync(
        TranslationSuggestion suggestion,
        ResourceEntry? entryTarget,
        EntityTranslation? translationTarget,
        CancellationToken ct);

    /// <summary>Persists any outbox messages buffered on the current context.</summary>
    Task FlushOutboxAsync(CancellationToken ct);
}

public sealed record NamespaceRow(
    Guid Id, string Name, string? Description, DateTime CreatedOn);

public sealed record EntryRow(
    Guid Id, Guid NamespaceId, string NamespaceName, string Key,
    bool IsDeprecated, DateTime? DeprecatedOn,
    IReadOnlyList<EntryRevisionRow> Revisions);

public sealed record EntryRevisionRow(
    Guid Id, string CultureCode, ReviewState State, string Provenance,
    Guid ProposedBy, DateTime ProposedOn, Guid? ReviewedBy, DateTime? ReviewedOn)
{
    /// <summary>Values never leave the store on list surfaces â€” metadata only
    /// (ADR-029 decision 10).</summary>
    public string? Value { get; init; }
}

public sealed record ExportCandidateRow(
    Guid EntryId, string NamespaceName, string Key,
    IReadOnlyDictionary<string, string> ApprovedValues);

public sealed record EntityTranslationRef(
    string SourceContext, string EntityType, Guid EntityId, string Field, string Culture);

public sealed record EntityTranslationRow(
    Guid Id, string SourceContext, string EntityType, Guid EntityId, string Field,
    string CultureCode, ReviewState State, string? Value,
    DateTime? ReviewedOn);

public sealed record SuggestionRow(
    Guid Id, string TargetKind, Guid TargetId, string TargetCultureCode,
    SuggestionStatus Status, string Provenance, DateTime CreatedOn)
{
    /// <summary>Suggested values ride only on the list surface for curators;
    /// they are still never logged.</summary>
    public string? SuggestedValue { get; init; }
}
