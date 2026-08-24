using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Localization.Application.Permissions;
using CommunityOS.Localization.Domain;
using CommunityOS.Localization.Domain.Events;
using CommunityOS.Localization.Domain.Exceptions;
using MediatR;

namespace CommunityOS.Localization.Application;

public sealed record ResourceEntryDto(
    Guid Id, Guid NamespaceId, string NamespaceName, string Key,
    bool IsDeprecated, DateTime? DeprecatedOn, DateTime CreatedOn, DateTime UpdatedOn);

public sealed record WalkEntriesQuery(
    Guid ActorId,
    Guid? NamespaceId,
    string? State,
    string? Search,
    string? Cursor,
    int Limit) : IRequest<IReadOnlyList<EntryRow>>;

/// <summary>Deterministic keyset walk over the catalog. List responses are
/// metadata-only: revision values never leave the store here (ADR-029
/// decision 10).</summary>
public sealed class WalkEntriesHandler(
    ILocalizationReader reader,
    AuthorizationGuard guard)
    : IRequestHandler<WalkEntriesQuery, IReadOnlyList<EntryRow>>
{
    public async Task<IReadOnlyList<EntryRow>> Handle(WalkEntriesQuery request, CancellationToken cancellationToken)
    {
        if (request.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(request.ActorId, LocalizationPermissions.ResourceRead, ct: cancellationToken);

        var cursor = EntryCursor.Decode(request.Cursor);
        return await reader.WalkEntriesAsync(
            request.NamespaceId, request.State?.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            cursor, request.Limit, cancellationToken);
    }
}

internal static class EntryCursor
{
    private const char Separator = '\u001f';

    public static string Encode(string namespaceName, string key, Guid id) =>
        Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{namespaceName}{Separator}{key}{Separator}{id}"));

    public static (string NamespaceName, string Key, Guid Id)? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        string decoded;
        try
        {
            decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        }
        catch (FormatException)
        {
            throw new ArgumentException("The pagination cursor is malformed.");
        }

        var parts = decoded.Split(Separator);
        if (parts.Length != 3 || !Guid.TryParse(parts[2], out var id))
        {
            throw new ArgumentException("The pagination cursor is malformed.");
        }

        return (parts[0], parts[1], id);
    }
}

public sealed record RevisionDto(
    Guid RevisionId, string CultureCode, string State, string Provenance,
    Guid ProposedBy, DateTime ProposedOn);

public sealed record CreateResourceEntryCommand(
    Guid ActorId, Guid NamespaceId, string Key, string Culture, string Value)
    : IRequest<ResourceEntryDto>;

/// <summary>Creates a catalog entry together with its first draft revision.
/// The target culture must be an active locale â€” content is never drafted for
/// locales that cannot resolve (ADR-029 decision 3).</summary>
public sealed class CreateResourceEntryHandler(
    ILocalizationReader reader,
    ILocalizationJournal journal,
    AuthorizationGuard guard)
    : IRequestHandler<CreateResourceEntryCommand, ResourceEntryDto>
{
    public async Task<ResourceEntryDto> Handle(CreateResourceEntryCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LocalizationPermissions.ResourcePropose, ct: cancellationToken);

        var @namespace = await reader.FindNamespaceAsync(command.NamespaceId, cancellationToken)
                         ?? throw new LocalizationNotFoundException("The requested namespace was not found.");

        await CreateResourceEntryHandler.RequireActiveCultureAsync(reader, command.Culture, cancellationToken);

        if (await reader.FindTrackedEntryByKeyAsync(@namespace.Id, command.Key.Trim(), cancellationToken) is not null)
        {
            throw new LocalizationConflictException(
                $"Key '{command.Key.Trim()}' already exists in this namespace.");
        }

        var entry = ResourceEntry.Create(command.NamespaceId, command.Key, DateTime.UtcNow);
        entry.ProposeDraft(
            command.Culture, command.Value, ContentProvenance.ForHuman(), command.ActorId, DateTime.UtcNow);
        await journal.SaveEntryAsync(entry, cancellationToken);
        return ToDto(entry, @namespace.Name);
    }

    internal static async Task RequireActiveCultureAsync(
        ILocalizationReader reader, string culture, CancellationToken ct)
    {
        var locale = await reader.FindLocaleAsync(Bcp47.Normalize(culture), ct)
                     ?? throw new LocalizationConflictException(
                         $"Culture '{Bcp47.Normalize(culture)}' is not registered in the locale registry.");
        if (locale.Status != LocaleStatus.Active && !locale.IsDefault)
        {
            throw new LocalizationConflictException(
                $"Culture '{locale.Code}' is not activated; activate it before proposing content.");
        }
    }

    internal static ResourceEntryDto ToDto(ResourceEntry entry, string namespaceName) =>
        new(entry.Id, entry.NamespaceId, namespaceName, entry.Key,
            entry.IsDeprecated, entry.DeprecatedOn, entry.CreatedOn, entry.UpdatedOn);
}

public sealed record AddRevisionResult(RevisionDto Revision);

public sealed record AddResourceRevisionCommand(
    Guid ActorId, Guid EntryId, string Culture, string Value) : IRequest<AddRevisionResult>;

/// <summary>Edits localized content by proposing a NEW draft revision; the
/// previously approved value remains verbatim until the successor passes
/// review (ADR-029 decision 4 â€” approved revisions are immutable).</summary>
public sealed class AddResourceRevisionHandler(
    ILocalizationReader reader,
    ILocalizationJournal journal,
    AuthorizationGuard guard)
    : IRequestHandler<AddResourceRevisionCommand, AddRevisionResult>
{
    public async Task<AddRevisionResult> Handle(AddResourceRevisionCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LocalizationPermissions.ResourcePropose, ct: cancellationToken);

        var entry = await reader.FindTrackedEntryAsync(command.EntryId, cancellationToken)
                    ?? throw new LocalizationNotFoundException();

        await CreateResourceEntryHandler.RequireActiveCultureAsync(reader, command.Culture, cancellationToken);

        var revision = entry.ProposeDraft(
            command.Culture, command.Value, ContentProvenance.ForHuman(), command.ActorId, DateTime.UtcNow);
        await journal.SaveEntryAsync(entry, cancellationToken);
        return new(new(revision.Id, revision.CultureCode,
            revision.State.ToString().ToLowerInvariant(), revision.Provenance,
            revision.ProposedBy, revision.ProposedOn));
    }
}

public sealed record TransitionResult(Guid EntryId, Guid RevisionId, string State);

public sealed record SubmitRevisionCommand(Guid ActorId, Guid EntryId, Guid RevisionId) : IRequest<TransitionResult>;

public sealed class SubmitRevisionHandler(
    ILocalizationReader reader,
    ILocalizationJournal journal,
    AuthorizationGuard guard)
    : IRequestHandler<SubmitRevisionCommand, TransitionResult>
{
    public async Task<TransitionResult> Handle(SubmitRevisionCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LocalizationPermissions.ResourcePropose, ct: cancellationToken);

        var entry = await EntryOperationGate.FindEntryWithRevisionAsync(
            reader, command.EntryId, command.RevisionId, cancellationToken);
        entry.SubmitForReview(command.RevisionId, DateTime.UtcNow);
        await journal.SaveEntryAsync(entry, cancellationToken);

        var revision = entry.Revisions.First(r => r.Id == command.RevisionId);
        return new(entry.Id, revision.Id, revision.State.ToString().ToLowerInvariant());
    }
}

public sealed record ApproveRevisionCommand(Guid ActorId, Guid EntryId, Guid RevisionId) : IRequest<TransitionResult>;

/// <summary>The publishing act of the resource catalog: approve transitions a
/// revision to Approved, supersedes any previous approved value for the same
/// culture verbatim, bumps the singleton catalog version inside the save
/// transaction and flushes the outbox-captured
/// <see cref="CatalogChangedDomainEvent"/> atomically with the mutation
/// (ADR-029 decisions 4/14/16). Requires <c>localization.resource.review</c>;
/// propose alone can never publish.</summary>
public sealed class ApproveRevisionHandler(
    ILocalizationReader reader,
    ILocalizationJournal journal,
    AuthorizationGuard guard,
    IPublisher publisher)
    : IRequestHandler<ApproveRevisionCommand, TransitionResult>
{
    public async Task<TransitionResult> Handle(ApproveRevisionCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LocalizationPermissions.ResourceReview, ct: cancellationToken);

        var entry = await EntryOperationGate.FindEntryWithRevisionAsync(
            reader, command.EntryId, command.RevisionId, cancellationToken);
        var namespaceName = await reader.FindNamespaceNameAsync(entry.NamespaceId, cancellationToken)
                            ?? throw new LocalizationNotFoundException("The requested namespace was not found.");

        entry.Approve(command.RevisionId, command.ActorId, DateTime.UtcNow);
        var culture = entry.Revisions.First(r => r.Id == command.RevisionId).CultureCode;

        Task PublishAsync(long bundleVersion, CancellationToken ct) =>
            publisher.Publish(new CatalogChangedDomainEvent(
                "resources", namespaceName, culture, bundleVersion), ct);

        await journal.PublishCatalogChangeAsync(entry, PublishAsync, cancellationToken);

        var revision = entry.Revisions.First(r => r.Id == command.RevisionId);
        return new(entry.Id, revision.Id, revision.State.ToString().ToLowerInvariant());
    }
}

public sealed record RejectRevisionCommand(Guid ActorId, Guid EntryId, Guid RevisionId) : IRequest<TransitionResult>;

public sealed class RejectRevisionHandler(
    ILocalizationReader reader,
    ILocalizationJournal journal,
    AuthorizationGuard guard)
    : IRequestHandler<RejectRevisionCommand, TransitionResult>
{
    public async Task<TransitionResult> Handle(RejectRevisionCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LocalizationPermissions.ResourceReview, ct: cancellationToken);

        var entry = await EntryOperationGate.FindEntryWithRevisionAsync(
            reader, command.EntryId, command.RevisionId, cancellationToken);
        entry.Reject(command.RevisionId, command.ActorId, DateTime.UtcNow);
        await journal.SaveEntryAsync(entry, cancellationToken);

        var revision = entry.Revisions.First(r => r.Id == command.RevisionId);
        return new(entry.Id, revision.Id, revision.State.ToString().ToLowerInvariant());
    }
}

public sealed record DeprecateEntryResult(Guid EntryId, bool IsDeprecated);

public sealed record DeprecateResourceEntryCommand(Guid ActorId, Guid EntryId) : IRequest<DeprecateEntryResult>;

/// <summary>Soft-deprecates a key: nothing is ever hard-deleted (ADR-029
/// decision 9). The catalog change event keeps downstream bundles aware.</summary>
public sealed class DeprecateResourceEntryHandler(
    ILocalizationReader reader,
    ILocalizationJournal journal,
    AuthorizationGuard guard,
    IPublisher publisher)
    : IRequestHandler<DeprecateResourceEntryCommand, DeprecateEntryResult>
{
    public async Task<DeprecateEntryResult> Handle(DeprecateResourceEntryCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LocalizationPermissions.ResourceReview, ct: cancellationToken);

        var entry = await reader.FindTrackedEntryAsync(command.EntryId, cancellationToken)
                    ?? throw new LocalizationNotFoundException();
        var namespaceName = await reader.FindNamespaceNameAsync(entry.NamespaceId, cancellationToken)
                            ?? throw new LocalizationNotFoundException("The requested namespace was not found.");

        entry.Deprecate(DateTime.UtcNow);

        Task PublishAsync(long bundleVersion, CancellationToken ct) =>
            publisher.Publish(new CatalogChangedDomainEvent(
                "resources", namespaceName, "*", bundleVersion), ct);

        await journal.PublishCatalogChangeAsync(entry, PublishAsync, cancellationToken);
        return new(entry.Id, entry.IsDeprecated);
    }
}

internal static class EntryOperationGate
{
    /// <summary>Loads the tracked aggregate and asserts the revision belongs to
    /// it â€” uniform 404 otherwise (no existence oracle for foreign ids).</summary>
    public static async Task<ResourceEntry> FindEntryWithRevisionAsync(
        ILocalizationReader reader, Guid entryId, Guid revisionId, CancellationToken ct)
    {
        var entry = await reader.FindTrackedEntryAsync(entryId, ct)
                    ?? throw new LocalizationNotFoundException();
        if (entry.Revisions.All(r => r.Id != revisionId))
        {
            throw new LocalizationNotFoundException();
        }

        return entry;
    }
}
