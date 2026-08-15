namespace CommunityOS.Contracts.Documents;

/// <summary>
/// Raised when a document is created. Carries the stable id, title, lifecycle
/// status, primary organization scope and the creator (a stable person id).
/// Payloads never contain binary content, secrets, filenames or names
/// (ADR-022). No PII is exported.
/// </summary>
public sealed record DocumentCreated(
    Guid DocumentId,
    string Title,
    string Status,
    Guid? OrganizationUnitId,
    string? OwnerType,
    Guid? OwnerId,
    Guid CreatedBy,
    DateTime OccurredOn);

/// <summary>
/// Raised when non-security metadata (title/description) or the organization
/// scope set changes. Scope and title details are read through the Documents
/// API; only stable identifiers and lifecycle state are exported.
/// </summary>
public sealed record DocumentMetadataUpdated(
    Guid DocumentId,
    string Status,
    Guid? OrganizationUnitId,
    DateTime OccurredOn);

/// <summary>
/// Raised when a new immutable version is added to a document. Carries the
/// version number, MIME type, size and content hash — never the bytes, the
/// filename or the object key (ADR-022).
/// </summary>
public sealed record DocumentVersionAdded(
    Guid DocumentId,
    Guid VersionId,
    int VersionNumber,
    string MimeType,
    long SizeBytes,
    string ContentHash,
    Guid UploadedBy,
    DateTime OccurredOn);

/// <summary>
/// Raised when classification metadata (classification code, sensitive flag,
/// retention category, hold references) changes. Carries the classification
/// code string and the sensitive operational gate. No content.
/// </summary>
public sealed record DocumentClassified(
    Guid DocumentId,
    string? ClassificationCode,
    bool IsSensitive,
    DateTime OccurredOn);

/// <summary>
/// Raised when a document transitions to <c>Archived</c>.
/// </summary>
public sealed record DocumentArchived(Guid DocumentId, DateTime OccurredOn);

/// <summary>
/// Raised when a document transitions to <c>Deactivated</c> (soft-delete).
/// Content is preserved and the transition is reversible by restore.
/// </summary>
public sealed record DocumentDeactivated(Guid DocumentId, DateTime OccurredOn);

/// <summary>
/// Raised when a document is restored from <c>Archived</c> or
/// <c>Deactivated</c> back to <c>Active</c>.
/// </summary>
public sealed record DocumentRestored(Guid DocumentId, string Status, DateTime OccurredOn);

/// <summary>
/// Raised when a malware scan of a version completes. Only raised when the
/// scan extension point is enabled. Carries the resulting scan status string.
/// </summary>
public sealed record DocumentScanCompleted(
    Guid DocumentId,
    Guid VersionId,
    string ScanStatus,
    DateTime OccurredOn);

/// <summary>
/// Raised when protected (sensitive) document content is actually downloaded.
/// Audit-oriented: targeted at future security/audit consumers. Never carries
/// binary content, secrets or unnecessary personal information. Ordinary
/// metadata reads are never published. A future Audit service may consume this
/// event; Documents does not depend directly on Audit (ADR-022, ratified).
/// </summary>
public sealed record DocumentContentDownloaded(
    Guid DocumentId,
    Guid VersionId,
    Guid ActorId,
    DateTime OccurredOn);