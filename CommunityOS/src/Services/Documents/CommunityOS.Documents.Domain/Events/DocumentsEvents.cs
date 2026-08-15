using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Documents.Domain.Events;

/// <summary>Raised when a document is created (lifecycle <c>Draft</c>).</summary>
public sealed record DocumentCreatedEvent(
    Guid DocumentId,
    string Title,
    string Status,
    Guid? OrganizationUnitId,
    string? OwnerType,
    Guid? OwnerId,
    Guid CreatedBy) : DomainEvent;

/// <summary>
/// Raised when non-security metadata or the organization scope set changes.
/// Metadata changes never create a version (ADR-022).
/// </summary>
public sealed record DocumentMetadataUpdatedEvent(
    Guid DocumentId,
    string Status,
    Guid? OrganizationUnitId) : DomainEvent;

/// <summary>Raised when a new immutable version is added.</summary>
public sealed record DocumentVersionAddedEvent(
    Guid DocumentId,
    Guid VersionId,
    int VersionNumber,
    string MimeType,
    long SizeBytes,
    string ContentHash,
    Guid UploadedBy) : DomainEvent;

/// <summary>Raised when classification/sensitive/retention/holds change.</summary>
public sealed record DocumentClassifiedEvent(
    Guid DocumentId,
    string? ClassificationCode,
    bool IsSensitive) : DomainEvent;

/// <summary>Raised on <c>Active → Archived</c>.</summary>
public sealed record DocumentArchivedEvent(Guid DocumentId) : DomainEvent;

/// <summary>Raised on <c>Active/Archived → Deactivated</c>.</summary>
public sealed record DocumentDeactivatedEvent(Guid DocumentId) : DomainEvent;

/// <summary>Raised on <c>Archived/Deactivated → Active</c>.</summary>
public sealed record DocumentRestoredEvent(Guid DocumentId, string Status) : DomainEvent;

/// <summary>Raised when a scan completes (scanning enabled only).</summary>
public sealed record DocumentScanCompletedEvent(
    Guid DocumentId,
    Guid VersionId,
    string ScanStatus) : DomainEvent;