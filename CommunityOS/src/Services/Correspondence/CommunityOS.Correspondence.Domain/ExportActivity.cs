namespace CommunityOS.Correspondence.Domain;

/// <summary>
/// Correspondence's internal export activity journal (ADR-028 decision 1):
/// who exported, when, in which format, with which bounded filter summary and
/// row count — never row contents. Standalone table; no foreign keys; never
/// expires.
/// </summary>
public sealed class ExportActivity
{
    private ExportActivity()
    {
    }

    public Guid Id { get; private set; }

    public Guid RequestedBy { get; private set; }

    public string Format { get; private set; } = null!;

    /// <summary>Compact canonical filter summary (ids/codes/timestamps only).</summary>
    public string FilterSummary { get; private set; } = null!;

    public int RowCount { get; private set; }

    public bool IncludedSensitive { get; private set; }

    public DateTime RequestedOn { get; private set; }

    public static ExportActivity Create(
        Guid requestedBy, string format, string filterSummary, int rowCount,
        bool includedSensitive, DateTime requestedOn) =>
        new()
        {
            Id = Guid.NewGuid(),
            RequestedBy = requestedBy,
            Format = format,
            FilterSummary = filterSummary,
            RowCount = rowCount,
            IncludedSensitive = includedSensitive,
            RequestedOn = requestedOn
        };
}
