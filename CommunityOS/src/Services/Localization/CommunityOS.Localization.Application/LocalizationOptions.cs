namespace CommunityOS.Localization.Application;

/// <summary>
/// Deployment options for the Localization service (ADR-029 decision 13).
/// All list surfaces are bounded; the machine-translation seam ships disabled
/// (ADR-029 decisions 19–20) — no provider is configured by default and the
/// service is fully functional without one.
/// </summary>
public sealed class LocalizationOptions
{
    public const string SectionName = "Localization";

    public int MaxPageSize { get; set; } = 100;

    public int DefaultPageSize { get; set; } = 25;

    /// <summary>Maximum number of items in a batch entity-translation upsert.</summary>
    public int MaxBatchUpsert { get; set; } = 200;

    /// <summary>Maximum number of keys resolved into one export bundle.</summary>
    public int MaxExportKeys { get; set; } = 10000;

    /// <summary>Maximum stored length of one localized value.</summary>
    public int MaxValueLength { get; set; } = 2000;
}
