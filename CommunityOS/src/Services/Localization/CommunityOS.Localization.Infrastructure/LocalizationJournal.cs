using CommunityOS.Localization.Application;
using CommunityOS.Localization.Domain;
using CommunityOS.Localization.Domain.Exceptions;
using CommunityOS.Localization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Localization.Infrastructure;

/// <summary>
/// Write-side implementation. Every save is ONE SaveChanges so
/// outbox-captured integration events commit atomically with catalog changes
/// (ADR-015, ADR-029 decisions 14/16). Publishing saves additionally bump the
/// singleton catalog version inside the same transaction and pass the NEW
/// version to the event callback before the flush, so
/// <c>LocalizationCatalogChanged</c> payloads always carry the committed
/// version. Nothing is ever hard-deleted (ADR-029 decision 9).
/// </summary>
public sealed class LocalizationJournal(LocalizationDbContext db, ILogger<LocalizationJournal> logger)
    : ILocalizationJournal
{
    public async Task SaveLocaleAsync(Locale locale, Locale? previousDefault, CancellationToken ct)
    {
        Attach(locale);
        if (previousDefault is not null)
        {
            Attach(previousDefault);
        }

        await db.SaveChangesAsync(ct);
        if (locale.IsDefault)
        {
            logger.DefaultLocaleSet(locale.Code);
        }
    }

    public async Task SaveNamespaceAsync(ResourceNamespace resourceNamespace, CancellationToken ct)
    {
        Attach(resourceNamespace);
        await db.SaveChangesAsync(ct);
        logger.NamespaceSaved(resourceNamespace.Id);
    }

    public Task SaveEntryAsync(ResourceEntry entry, CancellationToken ct) => SaveAggregateAsync(entry, ct);

    public Task SaveEntityTranslationAsync(EntityTranslation translation, CancellationToken ct) =>
        SaveAggregateAsync(translation, ct);

    public Task SaveSuggestionAsync(TranslationSuggestion suggestion, CancellationToken ct) =>
        SaveAggregateAsync(suggestion, ct);

    public async Task SaveSuggestionDecisionAsync(
        TranslationSuggestion suggestion,
        ResourceEntry? entryTarget,
        EntityTranslation? translationTarget,
        CancellationToken ct)
    {
        Attach(suggestion);
        if (entryTarget is not null)
        {
            Attach(entryTarget);
        }

        if (translationTarget is not null)
        {
            Attach(translationTarget);
        }

        await db.SaveChangesAsync(ct);
        logger.SuggestionDecided(suggestion.Id, suggestion.Status.ToString().ToLowerInvariant());
    }

    public Task FlushOutboxAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task PublishCatalogChangeAsync(
        ResourceEntry entry, Func<long, CancellationToken, Task> publishEvent, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var version = await BumpCatalogVersionAsync(ct);
            Attach(entry);
            await db.SaveChangesAsync(ct);

            // The bus outbox buffers the publish into this context; this second
            // SaveChanges flushes it inside the same transaction.
            await publishEvent(version, ct);
            await db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
            logger.CatalogPublished("resources", entry.Id, version);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw new LocalizationConflictException(
                "The catalog changed concurrently; retry the operation.");
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task PublishCatalogChangeAsync(
        EntityTranslation translation, Func<long, CancellationToken, Task> publishEvent, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var version = await BumpCatalogVersionAsync(ct);
            Attach(translation);
            await db.SaveChangesAsync(ct);

            await publishEvent(version, ct);
            await db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
            logger.CatalogPublished("entities", translation.Id, version);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw new LocalizationConflictException(
                "The catalog changed concurrently; retry the operation.");
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<long> BumpCatalogVersionAsync(CancellationToken ct)
    {
        var state = await db.CatalogState.SingleAsync(ct);
        state.Version += 1;
        state.UpdatedOn = DateTime.UtcNow;
        return state.Version;
    }

    private async Task SaveAggregateAsync<T>(T aggregate, CancellationToken ct) where T : class
    {
        if (db.Entry(aggregate).State == EntityState.Detached)
        {
            db.Add(aggregate);
        }
        else
        {
            db.Update(aggregate);
        }

        await db.SaveChangesAsync(ct);
    }

    private void Attach(object aggregate)
    {
        if (db.Entry(aggregate).State == EntityState.Detached)
        {
            db.Add(aggregate);
        }
    }
}

internal static partial class LocalizationJournalLog
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Catalog change published for {CatalogContext} target {TargetId}; global version {Version}.")]
    public static partial void CatalogPublished(
        this ILogger logger, string catalogContext, Guid targetId, long version);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Locale registry default set to {Code}.")]
    public static partial void DefaultLocaleSet(this ILogger logger, string code);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Resource namespace {NamespaceId} saved.")]
    public static partial void NamespaceSaved(this ILogger logger, Guid namespaceId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Suggestion {SuggestionId} decided with status {Status}.")]
    public static partial void SuggestionDecided(this ILogger logger, Guid suggestionId, string status);
}
