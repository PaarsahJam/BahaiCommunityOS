namespace CommunityOS.Records.Domain.Aggregates;

/// <summary>
/// Security metadata carried by every record (ADR-023), following the ratified
/// convention so the Data Classification Model can be consumed without schema
/// redesign. <see cref="ClassificationCode"/> is a reserved string, never a
/// fixed enum. <see cref="IsSensitive"/> is an operational access-control gate:
/// when true, sensitive record fields require <c>records.record.read.sensitive</c>.
/// <see cref="RetentionScheduleCode"/> references the active
/// <see cref="RetentionSchedule"/>. <see cref="RetentionExpiredOn"/> marks the
/// record for a ratified disposition review; expiry never destroys data.
/// </summary>
public sealed class RecordClassificationMetadata
{
    private RecordClassificationMetadata()
    {
    }

    /// <summary>Reserved for the ratified classification level code. No fixed set yet.</summary>
    public string? ClassificationCode { get; private set; }

    /// <summary>Operational gate: sensitive fields require an extra permission.</summary>
    public bool IsSensitive { get; private set; }

    public string? RetentionScheduleCode { get; private set; }

    /// <summary>Set when the retention period lapses; flags review, never destruction.</summary>
    public DateTime? RetentionExpiredOn { get; private set; }

    public Guid? ClassifiedBy { get; private set; }

    public DateTime? ClassifiedOn { get; private set; }

    public static RecordClassificationMetadata Create() => new();

    public void Classify(
        string? classificationCode,
        bool isSensitive,
        string? retentionScheduleCode,
        Guid classifiedBy,
        DateTime occurredOn)
    {
        ClassificationCode = string.IsNullOrWhiteSpace(classificationCode)
            ? null
            : classificationCode.Trim();
        IsSensitive = isSensitive;
        RetentionScheduleCode = string.IsNullOrWhiteSpace(retentionScheduleCode)
            ? null
            : retentionScheduleCode.Trim();
        ClassifiedBy = classifiedBy;
        ClassifiedOn = occurredOn.ToUniversalTime();
    }

    /// <summary>Flags the record for retention disposition review. Never destroys.</summary>
    public void FlagRetentionExpired(DateTime expiredOn) =>
        RetentionExpiredOn = expiredOn.ToUniversalTime();
}