using CommunityOS.Audit.Domain;

namespace CommunityOS.Audit.Application;

/// <summary>
/// One journal entry as read from the store, before authorization. Carries
/// everything the query/hold handlers need to build authorization contexts
/// and DTOs.
/// </summary>
public sealed record AuditEntryRow(
    Guid Id,
    string SourceService,
    string SourceEventType,
    string Action,
    string? Outcome,
    string ResourceType,
    Guid ResourceId,
    Guid? SecondaryResourceId,
    Guid? SubjectId,
    Guid? ActorId,
    Guid? OrganizationUnitId,
    AuditSensitivity Sensitivity,
    string? MetadataJson,
    string RetentionClass,
    DateTime? RetentionExpiresOn,
    DateTime OccurredOn,
    DateTime IngestedOn)
{
    public bool IsSensitive => Sensitivity == AuditSensitivity.Sensitive;
}

public sealed record PurgeBatchResult(int PurgedCount, int RemainingExpired);

/// <summary>
/// Fully mapped producer event ready to be journaled. Consumers build this via
/// the ratified ingest mappings; the ingestor applies the retention policy,
/// enforces hash idempotency and persists.
/// </summary>
public sealed record IngestCandidate(
    string SourceService,
    string SourceEventType,
    string Action,
    string ResourceType,
    Guid ResourceId,
    Guid? SecondaryResourceId,
    Guid? SubjectId,
    Guid? ActorId,
    Guid? OrganizationUnitId,
    AuditSensitivity Sensitivity,
    string? Outcome,
    AuditMetadata? Metadata,
    DateTime OccurredOn,
    string SourceEventHash);

public enum IngestOutcome
{
    Persisted,
    Duplicate
}
