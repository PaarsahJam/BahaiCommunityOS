using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Localization.Application.Permissions;
using CommunityOS.Localization.Domain;
using CommunityOS.Localization.Domain.Exceptions;
using MediatR;

namespace CommunityOS.Localization.Application;

public sealed record ListSuggestionsQuery(
    Guid ActorId, string? Status, int Limit) : IRequest<IReadOnlyList<SuggestionRow>>;

/// <summary>Suggestions are a curation surface â€” reading them requires the
/// review permission (ADR-029 decisions 13/19).</summary>
public sealed class ListSuggestionsHandler(
    ILocalizationReader reader,
    AuthorizationGuard guard)
    : IRequestHandler<ListSuggestionsQuery, IReadOnlyList<SuggestionRow>>
{
    public async Task<IReadOnlyList<SuggestionRow>> Handle(ListSuggestionsQuery request, CancellationToken cancellationToken)
    {
        if (request.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(request.ActorId, LocalizationPermissions.ResourceReview, ct: cancellationToken);

        return await reader.ListSuggestionsAsync(
            request.Status?.Trim().ToLowerInvariant(), request.Limit, cancellationToken);
    }
}

public sealed record SuggestionDecisionResult(
    Guid SuggestionId,
    string TargetKind,
    Guid TargetId,
    Guid? RevisionId,
    string? RevisionState);

public sealed record AcceptSuggestionIntoReviewCommand(Guid ActorId, Guid SuggestionId)
    : IRequest<SuggestionDecisionResult>;

/// <summary>
/// Curator accepts a suggestion into the human review workflow: the content
/// becomes an <see cref="ReviewState.InReview"/> revision on the target and
/// the suggestion is marked accepted â€” one atomic save. The acceptor acts as
/// proposer (propose permission); provenance of the original drafter is
/// preserved on the revision. This path can never publish: approval still
/// requires the separate review action by design (ADR-029 decision 19).
/// </summary>
public sealed class AcceptSuggestionIntoReviewHandler(
    ILocalizationReader reader,
    ILocalizationJournal journal,
    AuthorizationGuard guard)
    : IRequestHandler<AcceptSuggestionIntoReviewCommand, SuggestionDecisionResult>
{
    public async Task<SuggestionDecisionResult> Handle(AcceptSuggestionIntoReviewCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LocalizationPermissions.ResourcePropose, ct: cancellationToken);

        var suggestion = await SuggestionGate.RequirePendingSuggestionAsync(reader, command.SuggestionId, cancellationToken);
        var provenance = ContentProvenance.FromStored(suggestion.Provenance);
        var now = DateTime.UtcNow;

        ResourceEntry? entryTarget = null;
        EntityTranslation? translationTarget = null;
        Guid? revisionId = null;
        string? revisionState = null;

        switch (suggestion.TargetKind)
        {
            case "resource_entry":
                entryTarget = await reader.FindTrackedEntryAsync(suggestion.TargetId, cancellationToken)
                              ?? throw new LocalizationNotFoundException();
                var revision = entryTarget.ProposeDraft(
                    suggestion.TargetCultureCode, suggestion.SuggestedValue,
                    provenance, command.ActorId, now);
                entryTarget.SubmitForReview(revision.Id, now);
                revisionId = revision.Id;
                revisionState = revision.State.ToString().ToLowerInvariant();
                break;

            case "entity_translation":
                translationTarget = await reader.FindTrackedEntityTranslationAsync(suggestion.TargetId, cancellationToken)
                                    ?? throw new LocalizationNotFoundException();
                if (!translationTarget.CultureCode.Equals(suggestion.TargetCultureCode, StringComparison.Ordinal))
                {
                    throw new LocalizationConflictException(
                        "The suggestion culture no longer matches the target translation.");
                }

                var translationRevision = translationTarget.ProposeDraft(
                    suggestion.SuggestedValue, provenance, command.ActorId, now);
                translationTarget.SubmitForReview(translationRevision.Id, now);
                revisionId = translationRevision.Id;
                revisionState = translationRevision.State.ToString().ToLowerInvariant();
                break;

            default:
                throw new LocalizationConflictException(
                    $"Unknown suggestion target kind '{suggestion.TargetKind}'.");
        }

        suggestion.AcceptIntoReview(command.ActorId, now);
        await journal.SaveSuggestionDecisionAsync(suggestion, entryTarget, translationTarget, cancellationToken);

        return new(suggestion.Id, suggestion.TargetKind, suggestion.TargetId, revisionId, revisionState);
    }
}

public sealed record RejectSuggestionCommand(Guid ActorId, Guid SuggestionId)
    : IRequest<SuggestionDecisionResult>;

public sealed class RejectSuggestionHandler(
    ILocalizationReader reader,
    ILocalizationJournal journal,
    AuthorizationGuard guard)
    : IRequestHandler<RejectSuggestionCommand, SuggestionDecisionResult>
{
    public async Task<SuggestionDecisionResult> Handle(RejectSuggestionCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LocalizationPermissions.ResourceReview, ct: cancellationToken);

        var suggestion = await SuggestionGate.RequirePendingSuggestionAsync(reader, command.SuggestionId, cancellationToken);
        suggestion.Reject(command.ActorId, DateTime.UtcNow);
        await journal.SaveSuggestionDecisionAsync(suggestion, null, null, cancellationToken);

        return new(suggestion.Id, suggestion.TargetKind, suggestion.TargetId, null, null);
    }
}

internal static class SuggestionGate
{
    internal static async Task<TranslationSuggestion> RequirePendingSuggestionAsync(
        ILocalizationReader reader, Guid id, CancellationToken ct)
    {
        var suggestion = await reader.FindSuggestionAsync(id, ct)
                         ?? throw new LocalizationNotFoundException();
        if (suggestion.Status != SuggestionStatus.Pending)
        {
            throw new LocalizationConflictException("The suggestion has already been decided.");
        }

        return suggestion;
    }
}
