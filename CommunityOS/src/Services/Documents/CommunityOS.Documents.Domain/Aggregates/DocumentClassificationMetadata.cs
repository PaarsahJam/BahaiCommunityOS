namespace CommunityOS.Documents.Domain.Aggregates;

/// <summary>
/// Security metadata carried by every document (ADR-022). Designed so the
/// ratified Data Classification Model can be consumed without schema redesign:
/// <see cref="ClassificationCode"/> is a reserved string, never a fixed enum.
/// <see cref="IsSensitive"/> is an operational access-control gate, not a
/// classification level. Retention and hold fields are references only —
/// Records owns retention semantics.
/// </summary>
public sealed class DocumentClassificationMetadata
{
    private DocumentClassificationMetadata()
    {
    }

    /// <summary>Reserved for the ratified classification level code. No fixed set yet.</summary>
    public string? ClassificationCode { get; private set; }

    /// <summary>Operational gate: sensitive content requires an extra permission.</summary>
    public bool IsSensitive { get; private set; }

    public string? RetentionCategory { get; private set; }

    public string? LegalHoldReference { get; private set; }

    public string? AdministrativeHoldReference { get; private set; }

    public Guid? ClassifiedBy { get; private set; }

    public DateTime? ClassifiedOn { get; private set; }

    public static DocumentClassificationMetadata Create() => new();

    public void Classify(
        string? classificationCode,
        bool isSensitive,
        string? retentionCategory,
        string? legalHoldReference,
        string? administrativeHoldReference,
        Guid classifiedBy,
        DateTime occurredOn)
    {
        ClassificationCode = string.IsNullOrWhiteSpace(classificationCode)
            ? null
            : classificationCode.Trim();
        IsSensitive = isSensitive;
        RetentionCategory = string.IsNullOrWhiteSpace(retentionCategory)
            ? null
            : retentionCategory.Trim();
        LegalHoldReference = string.IsNullOrWhiteSpace(legalHoldReference)
            ? null
            : legalHoldReference.Trim();
        AdministrativeHoldReference = string.IsNullOrWhiteSpace(administrativeHoldReference)
            ? null
            : administrativeHoldReference.Trim();
        ClassifiedBy = classifiedBy;
        ClassifiedOn = occurredOn.ToUniversalTime();
    }

    /// <summary>True when a legal or administrative hold reference is present.</summary>
    public bool HasHoldReference =>
        !string.IsNullOrWhiteSpace(LegalHoldReference) ||
        !string.IsNullOrWhiteSpace(AdministrativeHoldReference);
}