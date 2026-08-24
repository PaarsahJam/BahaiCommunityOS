namespace CommunityOS.Localization.Domain;

/// <summary>Locale lifecycle (ADR-029 decision 3). A culture code may be
/// <see cref="LocaleStatus.Registered"/> without being usable for resolution;
/// only activated locales participate in the fallback chain, and exactly one
/// locale is the default.</summary>
public enum LocaleStatus
{
    Registered = 0,
    Active = 1
}

/// <summary>Revision lifecycle of a resource value or entity translation
/// (ADR-029 decisions 4 and 5). Approved revisions are immutable; a later
/// approval supersedes (never mutates) the previously approved revision.
/// <c>Superseded</c> is terminal bookkeeping for the displaced approved
/// revision — its stored value never changes.</summary>
public enum ReviewState
{
    Draft = 0,
    InReview = 1,
    Approved = 2,
    Rejected = 3,
    Superseded = 4
}

/// <summary>Provenance of proposed content (ADR-029 decisions 19 and 5):
/// human-authored or machine-drafted by a named provider. Machine provenance
/// never changes the review flow — approval always requires the ratified
/// human review action.</summary>
public sealed record ContentProvenance
{
    public const string Human = "human";

    private ContentProvenance(string value) => Value = value;

    public string Value { get; }

    public static ContentProvenance ForHuman() => new(Human);

    /// <summary>Machine provenance, e.g. "machine:azure-translator".</summary>
    public static ContentProvenance ForMachine(string providerCode)
    {
        if (string.IsNullOrWhiteSpace(providerCode))
        {
            throw new ArgumentException("Provider code is required for machine provenance.");
        }

        return new ContentProvenance($"machine:{providerCode.Trim().ToLowerInvariant()}");
    }

    public bool IsMachine => !Value.Equals(Human, StringComparison.Ordinal);

    /// <summary>Rehydrates provenance previously persisted as a string
    /// (suggestions carry their provenance across the accept-into-review
    /// boundary).</summary>
    public static ContentProvenance FromStored(string value)
    {
        var isHuman = string.Equals(value, Human, StringComparison.Ordinal);
        var isMachine = value.StartsWith("machine:", StringComparison.Ordinal) &&
                        value.Length > "machine:".Length;
        if (!isHuman && !isMachine)
        {
            throw new ArgumentException(
                $"'{value}' is not a valid content provenance; expected 'human' or 'machine:<provider>'.");
        }

        return new ContentProvenance(value.ToLowerInvariant());
    }

    public override string ToString() => Value;
}

/// <summary>Lifecycle state of an entity-translation suggestion awaiting
/// curator decision (ADR-029 decision 13). Accept moves the content into the
/// ordinary human review workflow in <see cref="ReviewState.InReview"/> — it
/// can never publish directly.</summary>
public enum SuggestionStatus
{
    Pending = 0,
    AcceptedIntoReview = 1,
    Rejected = 2
}
