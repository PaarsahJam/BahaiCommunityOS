namespace CommunityOS.Audit.Application;

/// <summary>
/// Audit service configuration (ADR-027). Bound from the <c>Audit</c>
/// configuration section.
/// </summary>
public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    /// <summary>Hard upper limit on the query <c>limit</c> parameter; larger
    /// values are clamped (ADR-027 decision 13).</summary>
    public int MaxPageSize { get; init; } = 100;

    /// <summary>Default page size when no limit is supplied.</summary>
    public int DefaultPageSize { get; init; } = 25;

    /// <summary>Hard row cap for synchronous exports; requests above it are
    /// rejected rather than clamped (ADR-027 decision 13).</summary>
    public int ExportMaxRows { get; init; } = 10_000;

    /// <summary>Default number of expired entries removed by one purge
    /// invocation.</summary>
    public int PurgeDefaultBatchSize { get; init; } = 500;

    /// <summary>Upper bound on a single purge batch (and therefore the
    /// operation, which removes one batch per invocation).</summary>
    public int PurgeMaxBatchSize { get; init; } = 5_000;

    /// <summary>Maximum number of entries addressable by one hold-placement
    /// request.</summary>
    public int HoldMaxBatchSize { get; init; } = 500;

    /// <summary>Retention class for audit-of-audit entries (exports, holds,
    /// purge markers). Retains indefinitely unless a duration is configured
    /// for this class, so purge history survives its own purge cycles.</summary>
    public string JournalClass { get; init; } = "audit-journal";

    /// <summary>Retention policy section (ADR-027 decision 14).</summary>
    public AuditRetentionOptions Retention { get; init; } = new();
}

/// <summary>
/// Deployment retention policy (ADR-027 decision 14): class codes map to
/// ISO-8601 durations; a class without a duration retains indefinitely.
/// Durations are deliberately deployment configuration — the architecture
/// specifies the mechanism, never the legal requirements.
/// </summary>
public sealed class AuditRetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>Class assigned to entries whose event type has no explicit
    /// mapping; retains indefinitely unless configured otherwise.</summary>
    public string DefaultClass { get; init; } = "default";

    /// <summary>Class code → ISO-8601 duration (e.g. "P7Y"); empty or missing
    /// duration = indefinite retention.</summary>
    public Dictionary<string, string?> Classes { get; init; } = new();

    /// <summary>Optional event-type → class-code overrides; unmatched event
    /// types receive <see cref="DefaultClass"/>.</summary>
    public Dictionary<string, string> EventClasses { get; init; } = new();
}
