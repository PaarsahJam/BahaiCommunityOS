namespace CommunityOS.Records.Application.Options;

/// <summary>
/// Application-level Records configuration (ratified, ADR-023). Bound from the
/// <c>Records</c> configuration section by the Infrastructure layer.
/// </summary>
public sealed class RecordsOptions
{
    public const string SectionName = "Records";

    public RetentionOptions Retention { get; set; } = new();
}

public sealed class RetentionOptions
{
    /// <summary>When true (default), retention expiry flags a review disposition; never destroys.</summary>
    public bool ReviewDispositionEnabled { get; set; } = true;
}