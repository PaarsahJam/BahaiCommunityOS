using SourceEventHashValidator = CommunityOS.Audit.Domain.SourceEventHash;

namespace CommunityOS.Audit.Domain;

/// <summary>
/// One immutable compliance-journal entry (ADR-027). An entry records that a
/// producer integration event happened: provenance (source service, event
/// type, content hash), the who/what/where as stable identifiers only, a
/// deterministic sensitivity classification, allowlisted scalar metadata and
/// the retention class assigned at ingest.
///
/// The aggregate is write-once by construction: every property has a private
/// setter, there are no mutation methods, and the only factory validates the
/// ratified field contracts. Corrections arrive as NEW events and therefore as
/// NEW entries — history is never rewritten through the application. Database
/// triggers provide the second, storage-level immutability layer; the sole
/// authorized purge path writes its marker entry inside the guarded
/// transaction before deleting expired rows.
/// </summary>
public sealed class AuditEntry
{
    private AuditEntry()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>When the producing event happened (producer clock).</summary>
    public DateTime OccurredOn { get; private set; }

    /// <summary>When the journal persisted the entry (Audit clock).</summary>
    public DateTime IngestedOn { get; private set; }

    /// <summary>Producing bounded context code (e.g. "records").</summary>
    public string SourceService { get; private set; } = null!;

    /// <summary>Integration event type name on the contract (e.g.
    /// "RecordVerified").</summary>
    public string SourceEventType { get; private set; } = null!;

    /// <summary>SHA-256 canonical identity of the producing event; unique.</summary>
    public string SourceEventHash { get; private set; } = null!;

    /// <summary>Stable action code derived from the event type (e.g.
    /// "record-verified").</summary>
    public string Action { get; private set; } = null!;

    /// <summary>Optional outcome code carried by the event (e.g. workflow
    /// completion outcome).</summary>
    public string? Outcome { get; private set; }

    public string ResourceType { get; private set; } = null!;
    public Guid ResourceId { get; private set; }

    /// <summary>Optional companion resource (hold id, evidence document,
    /// workflow domain entity, notification source).</summary>
    public Guid? SecondaryResourceId { get; private set; }

    /// <summary>The person the event is about, when the contract carries one.</summary>
    public Guid? SubjectId { get; private set; }

    /// <summary>The acting person, when the contract carries one.</summary>
    public Guid? ActorId { get; private set; }

    /// <summary>Primary organization scope of the event; null entries are
    /// visible only to global-scope holders (ADR-027 decision 11).</summary>
    public Guid? OrganizationUnitId { get; private set; }

    public AuditSensitivity Sensitivity { get; private set; }

    /// <summary>Reserved correlation identifier (ADR-027 decision 7) — always
    /// null at this gate until contracts carry ratified correlation fields.</summary>
    public Guid? CorrelationId { get; private set; }

    /// <summary>Reserved causation identifier — reserved alongside
    /// <see cref="CorrelationId"/>.</summary>
    public Guid? CausationId { get; private set; }

    /// <summary>Allowlisted scalar metadata as canonical JSON; null when the
    /// event contributes no metadata keys.</summary>
    public string? MetadataJson { get; private set; }

    /// <summary>Retention class code assigned at ingest from configuration.</summary>
    public string RetentionClass { get; private set; } = null!;

    /// <summary>When retention lapses; null means the class retains
    /// indefinitely and expiry never applies.</summary>
    public DateTime? RetentionExpiresOn { get; private set; }

    /// <summary>Creates a validated, terminal journal entry. There is no other
    /// construction path and no mutation path.</summary>
    public static AuditEntry Create(
        string sourceService,
        string sourceEventType,
        string action,
        string sourceEventHash,
        string resourceType,
        Guid resourceId,
        DateTime occurredOn,
        DateTime ingestedOn,
        AuditSensitivity sensitivity,
        string retentionClass,
        string? outcome = null,
        Guid? secondaryResourceId = null,
        Guid? subjectId = null,
        Guid? actorId = null,
        Guid? organizationUnitId = null,
        AuditMetadata? metadata = null,
        DateTime? retentionExpiresOn = null)
    {
        ValidateSourceService(sourceService);
        EventType(sourceEventType);
        ValidateAction(action);
        ValidateResourceType(resourceType);
        ValidateHash(sourceEventHash);
        ValidateIdentifier(resourceId, nameof(resourceId));
        ValidateOptionalIdentifier(secondaryResourceId, nameof(secondaryResourceId));
        ValidateOptionalIdentifier(subjectId, nameof(subjectId));
        ValidateOptionalIdentifier(actorId, nameof(actorId));
        ValidateOptionalIdentifier(organizationUnitId, nameof(organizationUnitId));
        ValidateRetention(retentionClass);

        return new AuditEntry
        {
            Id = Guid.NewGuid(),
            OccurredOn = occurredOn,
            IngestedOn = ingestedOn,
            SourceService = sourceService,
            SourceEventType = sourceEventType,
            Action = action,
            SourceEventHash = sourceEventHash,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Outcome = string.IsNullOrWhiteSpace(outcome) ? null : outcome.Trim(),
            SecondaryResourceId = secondaryResourceId,
            SubjectId = subjectId,
            ActorId = actorId,
            OrganizationUnitId = organizationUnitId,
            Sensitivity = sensitivity,
            MetadataJson = metadata is null || metadata.Values.Count == 0 ? null : metadata.Json,
            RetentionClass = retentionClass,
            RetentionExpiresOn = retentionExpiresOn
        };
    }

    private static void ValidateSourceService(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 50)
        {
            throw new ArgumentException("Audit source service must be 1..50 characters.", nameof(value));
        }
    }

    private static void EventType(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100)
        {
            throw new ArgumentException("Audit source event type must be 1..100 characters.", nameof(value));
        }
    }

    private static void ValidateAction(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100)
        {
            throw new ArgumentException("Audit action must be 1..100 characters.", nameof(value));
        }
    }

    private static void ValidateResourceType(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 50)
        {
            throw new ArgumentException("Audit resource type must be 1..50 characters.", nameof(value));
        }
    }

    private static void ValidateHash(string value)
    {
        if (!SourceEventHashValidator.IsValidShape(value))
        {
            throw new ArgumentException("Audit source event hash must be a lowercase SHA-256 hex digest.", nameof(value));
        }
    }

    private static void ValidateIdentifier(Guid value, string name)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"Audit identifier '{name}' must not be empty.", name);
        }
    }

    private static void ValidateOptionalIdentifier(Guid? value, string name)
    {
        if (value is { } id)
        {
            ValidateIdentifier(id, name);
        }
    }

    private static void ValidateRetention(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 50)
        {
            throw new ArgumentException("Audit retention class must be 1..50 characters.", nameof(value));
        }
    }
}
