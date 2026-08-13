using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Knowledge.Domain.Aggregates;

/// <summary>
/// An authoritative Library work (a book, tablet, or compilation). Works are
/// versioned by Editions, which are separate aggregates referencing the work id.
/// The Library is the single source of truth for citation text — community
/// content never embeds authoritative text, it references passages by id.
/// </summary>
public sealed class Work : AggregateRoot<Guid>
{
    private Work() : base(Guid.Empty)
    {
        Title = null!;
        OriginalLanguage = null!;
        DefaultLanguage = null!;
        WorkType = null!;
    }

    private Work(
        Guid id,
        string title,
        string originalLanguage,
        string defaultLanguage,
        string workType) : base(id)
    {
        Title = title;
        OriginalLanguage = originalLanguage;
        DefaultLanguage = defaultLanguage;
        WorkType = workType;
    }

    public string Title { get; private set; }
    public string OriginalLanguage { get; private set; }
    public string DefaultLanguage { get; private set; }
    public string WorkType { get; private set; }

    public static Work Create(string title, string workType, string originalLanguage, string defaultLanguage)
    {
        Guard.NotNullOrWhiteSpace(title, nameof(title));
        Guard.MaxLength(title, 300, nameof(title));
        Guard.NotNullOrWhiteSpace(workType, nameof(workType));
        Guard.MaxLength(workType, 50, nameof(workType));
        Guard.NotNullOrWhiteSpace(originalLanguage, nameof(originalLanguage));
        Guard.MaxLength(originalLanguage, 20, nameof(originalLanguage));
        Guard.NotNullOrWhiteSpace(defaultLanguage, nameof(defaultLanguage));
        Guard.MaxLength(defaultLanguage, 20, nameof(defaultLanguage));

        return new Work(Guid.NewGuid(), title.Trim(), originalLanguage.Trim(), defaultLanguage.Trim(), workType.Trim());
    }

    public void UpdateDetails(string title, string workType, string originalLanguage, string defaultLanguage)
    {
        Guard.NotNullOrWhiteSpace(title, nameof(title));
        Guard.MaxLength(title, 300, nameof(title));
        Guard.NotNullOrWhiteSpace(workType, nameof(workType));
        Guard.MaxLength(workType, 50, nameof(workType));
        Guard.NotNullOrWhiteSpace(originalLanguage, nameof(originalLanguage));
        Guard.MaxLength(originalLanguage, 20, nameof(originalLanguage));
        Guard.NotNullOrWhiteSpace(defaultLanguage, nameof(defaultLanguage));
        Guard.MaxLength(defaultLanguage, 20, nameof(defaultLanguage));

        Title = title.Trim();
        WorkType = workType.Trim();
        OriginalLanguage = originalLanguage.Trim();
        DefaultLanguage = defaultLanguage.Trim();
    }
}