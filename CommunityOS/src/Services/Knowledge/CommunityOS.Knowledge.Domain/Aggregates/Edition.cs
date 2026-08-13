using CommunityOS.Knowledge.Domain.Events;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Knowledge.Domain.Aggregates;

/// <summary>
/// A concrete published edition or translation of a Work (a language, a
/// translator, a publisher, an edition year). Carries the verification state
/// that gates whether community content may cite passages from this edition:
/// references may only target verified editions.
/// </summary>
public sealed class Edition : AggregateRoot<Guid>
{
    private Edition() : base(Guid.Empty)
    {
        Language = null!;
    }

    private Edition(
        Guid id,
        Guid workId,
        string language,
        string? translator,
        string? publisher,
        int? editionYear,
        bool verified) : base(id)
    {
        WorkId = workId;
        Language = language;
        Translator = translator;
        Publisher = publisher;
        EditionYear = editionYear;
        Verified = verified;
    }

    public Guid WorkId { get; private set; }
    public string Language { get; private set; }
    public string? Translator { get; private set; }
    public string? Publisher { get; private set; }
    public int? EditionYear { get; private set; }
    public bool Verified { get; private set; }

    public static Edition Import(
        Guid workId,
        string language,
        string? translator,
        string? publisher,
        int? editionYear,
        bool verified = false)
    {
        Guard.NotDefault(workId, nameof(workId));
        Guard.NotNullOrWhiteSpace(language, nameof(language));
        Guard.MaxLength(language, 20, nameof(language));
        Guard.MaxLength(translator ?? string.Empty, 200, nameof(translator));
        Guard.MaxLength(publisher ?? string.Empty, 300, nameof(publisher));
        Guard.PositiveOrZero(editionYear ?? 0, nameof(editionYear));

        var edition = new Edition(
            Guid.NewGuid(),
            workId,
            language.Trim(),
            TrimBlank(translator),
            TrimBlank(publisher),
            editionYear,
            verified);

        edition.RaiseDomainEvent(new EditionImportedEvent(
            edition.Id, edition.WorkId, edition.Language, edition.Verified));
        return edition;
    }

    /// <summary>
    /// Marks this edition verified so community content may cite its passages.
    /// </summary>
    public void Verify()
    {
        if (Verified) return;

        Verified = true;
        RaiseDomainEvent(new EditionVerifiedEvent(Id, WorkId));
    }

    private static string? TrimBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}