using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Records.Domain.Aggregates;

/// <summary>
/// One retention rule within a <see cref="RetentionSchedule"/> (ADR-023).
/// Retention is a time-based, category-scoped rule set; it never destroys
/// data — the disposition is a ratified review action. The period is an
/// ISO-8601 duration (for example <c>P3Y</c> or <c>P10Y</c>); an optional
/// maximum period caps total retention for records governed by this rule.
/// </summary>
public sealed class RetentionRule : Entity<Guid>
{
    private RetentionRule() : base(Guid.Empty)
    {
        Category = null!;
        StartTrigger = null!;
        Period = null!;
        Disposition = null!;
    }

    internal RetentionRule(
        Guid id,
        string category,
        string startTrigger,
        string period,
        string disposition,
        string? note,
        string? maximumPeriod) : base(id)
    {
        Category = category;
        StartTrigger = startTrigger;
        Period = period;
        Disposition = disposition;
        Note = note;
        MaximumPeriod = maximumPeriod;
    }

    /// <summary>Creates a retention rule for a category within a schedule.</summary>
    public static RetentionRule Create(
        string category,
        string startTrigger,
        string period,
        string disposition,
        string? note = null,
        string? maximumPeriod = null) =>
        new(Guid.NewGuid(), category, startTrigger, period, disposition, note, maximumPeriod);

    /// <summary>Category code this rule applies to. <c>*</c> means all categories.</summary>
    public string Category { get; private set; }

    /// <summary>When the retention clock starts: <c>recordDate</c> or <c>verifiedDate</c>.</summary>
    public string StartTrigger { get; private set; }

    /// <summary>ISO-8601 duration.</summary>
    public string Period { get; private set; }

    /// <summary>
    /// Optional ISO-8601 maximum that caps total retention. When present, a
    /// record governed by this rule is flagged for review once retention has
    /// run this long even if the rule's period would have expired earlier.
    /// </summary>
    public string? MaximumPeriod { get; private set; }

    /// <summary>Ratified disposition action on expiry: <c>review</c> (never destroys).</summary>
    public string Disposition { get; private set; }

    public string? Note { get; private set; }
}

/// <summary>
/// A named, versioned set of retention rules for record categories (ADR-023).
/// Records reference a schedule through <see cref="RecordClassificationMetadata.RetentionScheduleCode"/>.
/// A schedule in use by at least one record cannot be retired. Schedule code is
/// a stable string and is never renamed.
/// </summary>
public sealed class RetentionSchedule : AggregateRoot<Guid>
{
    private readonly List<RetentionRule> _rules = [];

    private RetentionSchedule() : base(Guid.Empty)
    {
        Code = null!;
        DisplayName = null!;
    }

    internal RetentionSchedule(Guid id, string code, string displayName, string? description, IReadOnlyList<RetentionRule> rules, Guid createdBy) : base(id)
    {
        Code = code;
        DisplayName = displayName;
        Description = description;
        _rules.AddRange(rules);
        CreatedBy = createdBy;
        CreatedOn = DateTime.UtcNow;
    }

    /// <summary>Stable string code referenced by records. Never renamed.</summary>
    public string Code { get; private set; }

    public string DisplayName { get; private set; }

    public string? Description { get; private set; }

    public bool IsRetired { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTime? UpdatedOn { get; private set; }

    public IReadOnlyList<RetentionRule> Rules => _rules.AsReadOnly();

    public static RetentionSchedule Create(
        string code,
        string displayName,
        string? description,
        IReadOnlyList<RetentionRule> rules,
        Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Schedule code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        var validRules = rules?.ToArray() ?? [];
        ValidateRules(validRules);

        return new RetentionSchedule(Guid.NewGuid(), code.Trim(), displayName.Trim(), description, validRules, createdBy);
    }

    public void Update(string displayName, string? description, IReadOnlyList<RetentionRule> rules, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        var validRules = rules?.ToArray() ?? [];
        ValidateRules(validRules);

        DisplayName = displayName.Trim();
        Description = description;
        _rules.Clear();
        _rules.AddRange(validRules);
        UpdatedBy = updatedBy;
        UpdatedOn = DateTime.UtcNow;
    }

    public void Retire(Guid retiredBy)
    {
        IsRetired = true;
        UpdatedBy = retiredBy;
        UpdatedOn = DateTime.UtcNow;
    }

    public void Reactivate(Guid reactivatedBy)
    {
        IsRetired = false;
        UpdatedBy = reactivatedBy;
        UpdatedOn = DateTime.UtcNow;
    }

    private static void ValidateRules(IReadOnlyList<RetentionRule> rules)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            if (!seen.Add(rule.Category))
                throw new ArgumentException($"Duplicate retention rule for category '{rule.Category}'.", nameof(rules));

            if (!System.Text.RegularExpressions.Regex.IsMatch(rule.Period, @"^P(?=\d|T\d)(?:\d+Y)?(?:\d+M)?(?:\d+W)?(?:\d+D)?(?:T(?:\d+H)?(?:\d+M)?(?:\d+S)?)?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                throw new Exceptions.InvalidRetentionPeriodException(rule.Period);

            if (!string.IsNullOrWhiteSpace(rule.MaximumPeriod) &&
                !System.Text.RegularExpressions.Regex.IsMatch(rule.MaximumPeriod, @"^P(?=\d|T\d)(?:\d+Y)?(?:\d+M)?(?:\d+W)?(?:\d+D)?(?:T(?:\d+H)?(?:\d+M)?(?:\d+S)?)?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                throw new Exceptions.InvalidRetentionPeriodException(rule.MaximumPeriod);
        }
    }
}