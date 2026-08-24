using CommunityOS.Localization.Domain.Exceptions;

namespace CommunityOS.Localization.Domain;

/// <summary>One immutable proposed/approved value of an entity translation.
/// Mirrors the resource revision lifecycle (ADR-029 decision 5).</summary>
public sealed class EntityTranslationRevision
{
    /// <summary>Hard bound for one localized string; mirrors
    /// LocalizationOptions.MaxValueLength.</summary>
    public const int ValueMaxLength = 2000;

    private EntityTranslationRevision()
    {
    }

    public Guid Id { get; private set; }

    public Guid TranslationId { get; private set; }

    public string CultureCode { get; private set; } = null!;

    /// <summary>The localized display value. Never logged.</summary>
    public string Value { get; private set; } = null!;

    public ReviewState State { get; private set; }

    public string Provenance { get; private set; } = null!;

    public Guid ProposedBy { get; private set; }

    public DateTime ProposedOn { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public DateTime? ReviewedOn { get; private set; }

    internal static EntityTranslationRevision Draft(
        Guid translationId, string culture, string value, ContentProvenance provenance,
        Guid proposedBy, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty translated value is required.");
        }

        if (value.Length > ValueMaxLength)
        {
            throw new ArgumentException(
                $"Translated values are limited to {ValueMaxLength} characters.");
        }

        return new EntityTranslationRevision
        {
            Id = Guid.NewGuid(),
            TranslationId = translationId,
            CultureCode = culture,
            Value = value,
            State = ReviewState.Draft,
            Provenance = provenance.Value,
            ProposedBy = proposedBy,
            ProposedOn = now
        };
    }

    internal void Transition(ReviewState target, DateTime now)
    {
        var legal = (State, target) switch
        {
            (ReviewState.Draft, ReviewState.InReview) => true,
            (ReviewState.InReview, ReviewState.Approved) => true,
            (ReviewState.InReview, ReviewState.Rejected) => true,
            (ReviewState.Approved, ReviewState.Superseded) => true,
            _ => false
        };

        if (!legal)
        {
            throw new LocalizationConflictException(
                $"Illegal review transition {State} â†’ {target}.");
        }

        State = target;
    }

    internal void MarkReviewed(Guid reviewer, DateTime now)
    {
        ReviewedBy = reviewer;
        ReviewedOn = now;
    }
}
