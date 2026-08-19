using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Records.Domain.Events;

/// <summary>Raised when a Draft record is created (ADR-023).</summary>
public sealed record RecordCreatedEvent(
    Guid RecordId,
    string Category,
    string Status,
    string SubjectType,
    Guid SubjectId,
    Guid? OrganizationUnitId,
    Guid CreatedBy) : DomainEvent;

/// <summary>Raised on <c>Draft → Submitted</c>.</summary>
public sealed record RecordSubmittedEvent(Guid RecordId, string Status) : DomainEvent;

/// <summary>Raised on <c>Submitted → Under Review</c>.</summary>
public sealed record RecordUnderReviewEvent(Guid RecordId, Guid ReviewerId) : DomainEvent;

/// <summary>Raised on <c>Under Review → Verified</c>.</summary>
public sealed record RecordVerifiedEvent(Guid RecordId, Guid VerifiedBy) : DomainEvent;

/// <summary>Raised on <c>Under Review → Rejected</c>.</summary>
public sealed record RecordRejectedEvent(Guid RecordId, Guid RejectedBy) : DomainEvent;

/// <summary>Raised when a post-verification correction appends a superseding version.</summary>
public sealed record RecordCorrectedEvent(
    Guid RecordId,
    int VersionNumber,
    int? SupersedesVersionNumber,
    Guid CorrectedBy) : DomainEvent;

/// <summary>Raised on <c>Verified → Archived</c>.</summary>
public sealed record RecordArchivedEvent(Guid RecordId) : DomainEvent;

/// <summary>Raised on transition to <c>Deactivated</c> (soft-delete).</summary>
public sealed record RecordDeactivatedEvent(Guid RecordId) : DomainEvent;

/// <summary>Raised on restore from <c>Archived</c>/<c>Deactivated</c>.</summary>
public sealed record RecordRestoredEvent(Guid RecordId, string Status) : DomainEvent;

/// <summary>Raised when classification/sensitive/retention metadata changes.</summary>
public sealed record RecordClassifiedEvent(
    Guid RecordId,
    string? ClassificationCode,
    bool IsSensitive) : DomainEvent;

/// <summary>Raised when the retention schedule/period reference changes.</summary>
public sealed record RecordRetentionChangedEvent(
    Guid RecordId,
    string? RetentionScheduleCode,
    string? RetentionPeriod) : DomainEvent;

/// <summary>Raised when a legal/administrative hold is placed.</summary>
public sealed record RecordHoldPlacedEvent(
    Guid HoldId,
    Guid RecordId,
    string HoldType,
    Guid PlacedBy) : DomainEvent;

/// <summary>Raised when a hold is released (releaser differs from placer).</summary>
public sealed record RecordHoldReleasedEvent(
    Guid HoldId,
    Guid RecordId,
    string HoldType,
    Guid ReleasedBy) : DomainEvent;

/// <summary>Raised when a document version is attached as evidence.</summary>
public sealed record RecordEvidenceAttachedEvent(
    Guid RecordId,
    Guid DocumentId,
    int VersionNumber,
    string ReferenceType) : DomainEvent;

/// <summary>Raised when an evidence reference is removed.</summary>
public sealed record RecordEvidenceRemovedEvent(
    Guid RecordId,
    Guid DocumentId,
    int VersionNumber) : DomainEvent;

/// <summary>Raised when retention expiry is flagged for review (never destroys).</summary>
public sealed record RecordRetentionExpiredEvent(
    Guid RecordId,
    string RetentionScheduleCode,
    DateTime ExpiredOn) : DomainEvent;