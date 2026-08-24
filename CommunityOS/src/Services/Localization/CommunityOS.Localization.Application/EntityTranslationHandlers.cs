using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Localization.Application.Permissions;
using CommunityOS.Localization.Domain;
using CommunityOS.Localization.Domain.Events;
using CommunityOS.Localization.Domain.Exceptions;
using MediatR;

namespace CommunityOS.Localization.Application;

/// <summary>Resolved value rows for by-reference entity-field translations
/// (ADR-029 decision 7). Values are returned here â€” this is the rendering
/// surface client applications call when composing entity views.</summary>
public sealed record QueryEntityTranslationsCommand(
    Guid ActorId,
    IReadOnlyList<EntityTranslationRef> Refs,
    bool IncludePending) : IRequest<IReadOnlyList<EntityTranslationRow>>;

public sealed class QueryEntityTranslationsHandler(
    ILocalizationReader reader,
    AuthorizationGuard guard)
    : IRequestHandler<QueryEntityTranslationsCommand, IReadOnlyList<EntityTranslationRow>>
{
    public async Task<IReadOnlyList<EntityTranslationRow>> Handle(
        QueryEntityTranslationsCommand request, CancellationToken cancellationToken)
    {
        if (request.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(request.ActorId, LocalizationPermissions.ResourceRead, ct: cancellationToken);

        return await reader.QueryEntityTranslationsAsync(
            request.Refs, request.IncludePending, request.Refs.Count, cancellationToken);
    }
}

public sealed record UpsertEntityTranslationItem(
    string SourceContext, string EntityType, Guid EntityId, string Field, string Culture, string Value);

public sealed record UpsertEntityTranslationsResult(int Created, int Updated);

public sealed record UpsertEntityTranslationsCommand(
    Guid ActorId, IReadOnlyList<UpsertEntityTranslationItem> Items)
    : IRequest<UpsertEntityTranslationsResult>;

/// <summary>
/// Batch upsert of by-reference translations. The owning service keeps its
/// entities untouched; Localization only stores field values keyed by the
/// (sourceContext, entityType, entityId, field, culture) tuple (ADR-029
/// decision 7). Reserved source contexts are rejected by the domain. Each
/// item becomes a NEW draft revision on the tuple's aggregate; previously
/// approved values stay verbatim until review publishes them.
/// </summary>
public sealed class UpsertEntityTranslationsHandler(
    ILocalizationReader reader,
    ILocalizationJournal journal,
    AuthorizationGuard guard)
    : IRequestHandler<UpsertEntityTranslationsCommand, UpsertEntityTranslationsResult>
{
    public async Task<UpsertEntityTranslationsResult> Handle(
        UpsertEntityTranslationsCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LocalizationPermissions.ResourcePropose, ct: cancellationToken);

        var tracked = new Dictionary<string, EntityTranslation>(StringComparer.Ordinal);
        var created = 0;
        var updated = 0;

        foreach (var item in command.Items)
        {
            var sourceContext = item.SourceContext.Trim().ToLowerInvariant();
            var entityType = item.EntityType.Trim().ToLowerInvariant();
            var field = item.Field.Trim().ToLowerInvariant();
            var culture = Bcp47.Normalize(item.Culture);
            var key = TupleKey(sourceContext, entityType, item.EntityId, field, culture);

            var existing = tracked.TryGetValue(key, out var aggregate)
                ? aggregate
                : await reader.FindEntityTranslationAsync(
                    sourceContext, entityType, item.EntityId, field, culture, cancellationToken);

            if (existing is null)
            {
                existing = EntityTranslation.Create(
                    sourceContext, entityType, item.EntityId, field, culture, DateTime.UtcNow);
                created++;
            }
            else
            {
                updated++;
            }

            existing.ProposeDraft(
                item.Value, ContentProvenance.ForHuman(), command.ActorId, DateTime.UtcNow);
            tracked[key] = existing;
        }

        foreach (var aggregate in tracked.Values)
        {
            await journal.SaveEntityTranslationAsync(aggregate, cancellationToken);
        }

        return new(created, updated);
    }

    private static string TupleKey(
        string sourceContext, string entityType, Guid entityId, string field, string culture) =>
        $"{sourceContext}|{entityType}|{entityId}|{field}|{culture}";
}

public sealed record TransitionStateResult(Guid Id, Guid RevisionId, string State);

public sealed record SubmitEntityTranslationCommand(Guid ActorId, Guid Id) : IRequest<TransitionStateResult>;

/// <summary>Submits the translation's newest draft revision for review.</summary>
public sealed class SubmitEntityTranslationHandler(
    ILocalizationReader reader,
    ILocalizationJournal journal,
    AuthorizationGuard guard)
    : IRequestHandler<SubmitEntityTranslationCommand, TransitionStateResult>
{
    public async Task<TransitionStateResult> Handle(SubmitEntityTranslationCommand request, CancellationToken cancellationToken)
    {
        if (request.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(request.ActorId, LocalizationPermissions.ResourcePropose, ct: cancellationToken);

        var translation = await TranslationOperationGate.RequireTranslationAsync(reader, request.Id, cancellationToken);
        var revision = TranslationOperationGate.RequireRevisionInState(translation, ReviewState.Draft);

        translation.SubmitForReview(revision.Id, DateTime.UtcNow);
        await journal.SaveEntityTranslationAsync(translation, cancellationToken);
        return new(translation.Id, revision.Id, revision.State.ToString().ToLowerInvariant());
    }
}

public sealed record ApproveEntityTranslationCommand(Guid ActorId, Guid Id) : IRequest<TransitionStateResult>;

/// <summary>Publishing act for entity translations: mirrors the resource flow
/// â€” approve supersedes verbatim, bumps the catalog version and flushes the
/// outbox-captured <see cref="CatalogChangedDomainEvent"/> atomically
/// (ADR-029 decisions 14/16).</summary>
public sealed class ApproveEntityTranslationHandler(
    ILocalizationReader reader,
    ILocalizationJournal journal,
    AuthorizationGuard guard,
    IPublisher publisher)
    : IRequestHandler<ApproveEntityTranslationCommand, TransitionStateResult>
{
    public async Task<TransitionStateResult> Handle(ApproveEntityTranslationCommand request, CancellationToken cancellationToken)
    {
        if (request.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(request.ActorId, LocalizationPermissions.ResourceReview, ct: cancellationToken);

        var translation = await TranslationOperationGate.RequireTranslationAsync(reader, request.Id, cancellationToken);
        var revision = TranslationOperationGate.RequireRevisionInState(translation, ReviewState.InReview);

        translation.Approve(revision.Id, request.ActorId, DateTime.UtcNow);

        Task PublishAsync(long bundleVersion, CancellationToken ct) =>
            publisher.Publish(new CatalogChangedDomainEvent(
                translation.SourceContext,
                $"{translation.EntityType}.{translation.Field}",
                translation.CultureCode,
                bundleVersion), ct);

        await journal.PublishCatalogChangeAsync(translation, PublishAsync, cancellationToken);
        return new(translation.Id, revision.Id, revision.State.ToString().ToLowerInvariant());
    }
}

public sealed record RejectEntityTranslationCommand(Guid ActorId, Guid Id) : IRequest<TransitionStateResult>;

public sealed class RejectEntityTranslationHandler(
    ILocalizationReader reader,
    ILocalizationJournal journal,
    AuthorizationGuard guard)
    : IRequestHandler<RejectEntityTranslationCommand, TransitionStateResult>
{
    public async Task<TransitionStateResult> Handle(RejectEntityTranslationCommand request, CancellationToken cancellationToken)
    {
        if (request.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(request.ActorId, LocalizationPermissions.ResourceReview, ct: cancellationToken);

        var translation = await TranslationOperationGate.RequireTranslationAsync(reader, request.Id, cancellationToken);
        var revision = TranslationOperationGate.RequireRevisionInState(translation, ReviewState.InReview);

        translation.Reject(revision.Id, request.ActorId, DateTime.UtcNow);
        await journal.SaveEntityTranslationAsync(translation, cancellationToken);
        return new(translation.Id, revision.Id, revision.State.ToString().ToLowerInvariant());
    }
}

public sealed record DeprecateEntityTranslationResult(Guid Id, bool IsDeprecated);

public sealed record DeprecateEntityTranslationCommand(Guid ActorId, Guid Id)
    : IRequest<DeprecateEntityTranslationResult>;

public sealed class DeprecateEntityTranslationHandler(
    ILocalizationReader reader,
    ILocalizationJournal journal,
    AuthorizationGuard guard,
    IPublisher publisher)
    : IRequestHandler<DeprecateEntityTranslationCommand, DeprecateEntityTranslationResult>
{
    public async Task<DeprecateEntityTranslationResult> Handle(DeprecateEntityTranslationCommand request, CancellationToken cancellationToken)
    {
        if (request.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(request.ActorId, LocalizationPermissions.ResourceReview, ct: cancellationToken);

        var translation = await TranslationOperationGate.RequireTranslationAsync(reader, request.Id, cancellationToken);
        translation.Deprecate(DateTime.UtcNow);

        Task PublishAsync(long bundleVersion, CancellationToken ct) =>
            publisher.Publish(new CatalogChangedDomainEvent(
                translation.SourceContext,
                $"{translation.EntityType}.{translation.Field}",
                "*",
                bundleVersion), ct);

        await journal.PublishCatalogChangeAsync(translation, PublishAsync, cancellationToken);
        return new(translation.Id, translation.IsDeprecated);
    }
}

internal static class TranslationOperationGate
{
    public static async Task<EntityTranslation> RequireTranslationAsync(
        ILocalizationReader reader, Guid id, CancellationToken ct)
    {
        var translation = await reader.FindTrackedEntityTranslationAsync(id, ct)
                          ?? throw new LocalizationNotFoundException();
        if (translation.IsDeprecated)
        {
            throw new LocalizationConflictException("The translation is deprecated.");
        }

        return translation;
    }

    /// <summary>Newest revision of the aggregate currently in the expected
    /// state; uniform 409 when no such revision exists.</summary>
    internal static EntityTranslationRevision RequireRevisionInState(
        EntityTranslation translation, ReviewState state)
    {
        var revision = translation.Revisions.LastOrDefault(r => r.State == state)
                       ?? throw new LocalizationConflictException(
                           $"The translation has no {state.ToString().ToLowerInvariant()} revision to act on.");
        return revision;
    }
}
