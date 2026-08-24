using CommunityOS.Localization.Domain.Exceptions;

namespace CommunityOS.Localization.Domain;

/// <summary>One immutable proposed/approved value of a resource entry for a
/// culture (ADR-029 decision 4). Approved rows are append-only history: they
/// are never edited â€” a later approval supersedes them.</summary>
public sealed class ResourceRevision
{
    /// <summary>Hard bound for one localized string; mirrors
    /// LocalizationOptions.MaxValueLength.</summary>
    public const int ValueMaxLength = 2000;

    private ResourceRevision()
    {
    }

    public Guid Id { get; private set; }

    public Guid EntryId { get; private set; }

    /// <summary>BCP-47 code of the culture this revision localizes.</summary>
    public string CultureCode { get; private set; } = null!;

    /// <summary>The localized string. Never logged.</summary>
    public string Value { get; private set; } = null!;

    public ReviewState State { get; private set; }

    /// <summary>"human" or "machine:&lt;provider&gt;" (ADR-029 decision 19).</summary>
    public string Provenance { get; private set; } = null!;

    public Guid ProposedBy { get; private set; }

    public DateTime ProposedOn { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public DateTime? ReviewedOn { get; private set; }

    internal static ResourceRevision Draft(
        Guid entryId, string culture, string value, ContentProvenance provenance,
        Guid proposedBy, DateTime now)
    {
        if (!Bcp47.IsValid(culture))
        {
            throw new ArgumentException($"'{culture}' is not a valid BCP-47 culture code.");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty resource value is required.");
        }

        if (value.Length > ValueMaxLength)
        {
            throw new ArgumentException(
                $"Resource values are limited to {ValueMaxLength} characters.");
        }

        return new ResourceRevision
        {
            Id = Guid.NewGuid(),
            EntryId = entryId,
            CultureCode = Bcp47.Normalize(culture),
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
