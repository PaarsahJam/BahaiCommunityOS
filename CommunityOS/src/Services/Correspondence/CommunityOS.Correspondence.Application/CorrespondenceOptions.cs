namespace CommunityOS.Correspondence.Application;

/// <summary>
/// Correspondence service configuration (ADR-028 decision 18). Bound from the
/// <c>Correspondence</c> configuration section.
/// </summary>
public sealed class CorrespondenceOptions
{
    public const string SectionName = "Correspondence";

    /// <summary>Hard upper limit on the query <c>limit</c> parameter; larger
    /// values are clamped (ADR-028).</summary>
    public int MaxPageSize { get; init; } = 100;

    /// <summary>Default page size when no limit is supplied.</summary>
    public int DefaultPageSize { get; init; } = 25;

    /// <summary>Hard row cap for synchronous exports; requests above it are
    /// rejected rather than clamped (ADR-028, ratified reject-not-clamp
    /// semantics for exports).</summary>
    public int ExportMaxRows { get; init; } = 10_000;

    /// <summary>Default number of expired letters removed by one purge
    /// invocation.</summary>
    public int PurgeDefaultBatchSize { get; init; } = 500;

    /// <summary>Upper bound on a single purge batch.</summary>
    public int PurgeMaxBatchSize { get; init; } = 5_000;

    /// <summary>Maximum number of letters addressable by one hold-placement
    /// request.</summary>
    public int HoldMaxBatchSize { get; init; } = 200;

    /// <summary>Submitted letters stranded beyond this window are flagged by
    /// the materialization reconciliation endpoint (ADR-028 decision 7).</summary>
    public int MaterializationWarnMinutes { get; init; } = 60;

    /// <summary>Retention policy section: classes are deployment
    /// configuration; default is indefinite retention.</summary>
    public CorrespondenceRetentionOptions Retention { get; init; } = new();
}

/// <summary>
/// Deployment retention policy (ADR-028 decision 13): class codes map to
/// ISO-8601 durations; a class without a duration retains indefinitely.
/// </summary>
public sealed class CorrespondenceRetentionOptions
{
    /// <summary>Class assigned to letters with no category override; retains
    /// indefinitely unless configured otherwise.</summary>
    public string DefaultClass { get; init; } = "default";

    /// <summary>Class code → ISO-8601 duration (e.g. "P7Y"); empty or missing
    /// duration = indefinite retention.</summary>
    public Dictionary<string, string?> Classes { get; init; } = new();

    /// <summary>Category-code → class-code overrides; unmatched categories
    /// receive <see cref="DefaultClass"/>.</summary>
    public Dictionary<string, string> EventClasses { get; init; } = new();
}
