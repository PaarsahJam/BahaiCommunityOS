using CommunityOS.Localization.Domain.Exceptions;

namespace CommunityOS.Localization.Domain;

/// <summary>
/// A proposed value awaiting curator decision (ADR-029 decision 13). Machine
/// provenance is recorded on the suggestion and carried onto any revision it
/// spawns, but acceptance never publishes: an accepted suggestion enters the
/// ordinary review workflow as <see cref="ReviewState.InReview"/> content, so
/// a separate human approval action is always required before anything is
/// visible (ADR-029 decision 19 â€” AI suggestions are human-approved drafts
/// only).
/// </summary>
public sealed class TranslationSuggestion
{
    private TranslationSuggestion()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>"resource_entry" or "entity_translation".</summary>
    public string TargetKind { get; private set; } = null!;

    /// <summary>ResourceEntry.Id or EntityTranslation.Id.</summary>
    public Guid TargetId { get; private set; }

    public string TargetCultureCode { get; private set; } = null!;

    /// <summary>The suggested value. Never logged.</summary>
    public string SuggestedValue { get; private set; } = null!;

    public string Provenance { get; private set; } = null!;

    public SuggestionStatus Status { get; private set; }

    public Guid? DecidedBy { get; private set; }

    public DateTime? DecidedOn { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public static TranslationSuggestion Create(
        string targetKind, Guid targetId, string culture, string suggestedValue,
        ContentProvenance provenance, DateTime now)
    {
        if (targetKind is not ("resource_entry" or "entity_translation"))
        {
            throw new ArgumentException(
                "Target kind must be 'resource_entry' or 'entity_translation'.");
        }

        if (!Bcp47.IsValid(culture))
        {
            throw new ArgumentException($"'{culture}' is not a valid BCP-47 culture code.");
        }

        if (string.IsNullOrWhiteSpace(suggestedValue))
        {
            throw new ArgumentException("A non-empty suggested value is required.");
        }

        return new TranslationSuggestion
        {
            Id = Guid.NewGuid(),
            TargetKind = targetKind,
            TargetId = targetId,
            TargetCultureCode = Bcp47.Normalize(culture),
            SuggestedValue = suggestedValue,
            Provenance = provenance.Value,
            Status = SuggestionStatus.Pending,
            CreatedOn = now
        };
    }

    /// <summary>Curator accepts the suggestion into the human review workflow:
    /// the caller turns the accepted content into an InReview revision on the
    /// target in the same save. The suggestion itself can never publish.</summary>
    public void AcceptIntoReview(Guid decidedBy, DateTime now)
    {
        EnsurePending();
        Status = SuggestionStatus.AcceptedIntoReview;
        MarkDecided(decidedBy, now);
    }

    public void Reject(Guid decidedBy, DateTime now)
    {
        EnsurePending();
        Status = SuggestionStatus.Rejected;
        MarkDecided(decidedBy, now);
    }

    private void EnsurePending()
    {
        if (Status != SuggestionStatus.Pending)
        {
            throw new LocalizationConflictException("The suggestion has already been decided.");
        }
    }

    private void MarkDecided(Guid decidedBy, DateTime now)
    {
        if (decidedBy == Guid.Empty)
        {
            throw new ArgumentException("The deciding subject is required.");
        }

        DecidedBy = decidedBy;
        DecidedOn = now;
    }
}
