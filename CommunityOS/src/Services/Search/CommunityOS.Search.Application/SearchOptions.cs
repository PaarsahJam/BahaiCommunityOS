namespace CommunityOS.Search.Application;

/// <summary>
/// Search service configuration (ADR-026). Bound from the
/// <c>Search</c> configuration section.
/// </summary>
public sealed class SearchOptions
{
    public const string SectionName = "Search";

    /// <summary>PostgreSQL text-search configuration used for stemming.</summary>
    public string DefaultLanguage { get; init; } = "english";

    /// <summary>Hard upper limit on the <c>limit</c> query parameter.</summary>
    public int MaxResultsPerPage { get; init; } = 50;
}
