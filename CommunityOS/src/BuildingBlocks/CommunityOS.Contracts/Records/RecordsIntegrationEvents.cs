namespace CommunityOS.Contracts.Records;

/// <summary>
/// Raised when a Draft record is created. Carries the stable id, category,
/// lifecycle status, subject reference (stable person/household id — never a
/// name or PII), primary organization scope and the creator (a stable person
/// id). Payloads never contain field values, secrets, names or hold reasons
/// (ADR-023).
/// </summary>
public sealed record RecordCreated(
    Guid RecordId,
    string Category,
    string Status,
    string SubjectType,
    Guid SubjectId,
    Guid? OrganizationUnitId,
    Guid CreatedBy,
    DateTime OccurredOn);

/// <summary>
/// Raised when a record transitions <c>Draft → Submitted</c>.
/// </summary>
public sealed record RecordSubmitted(
    Guid RecordId,
    string Status,
    DateTime OccurredOn);

/// <summary>
/// Raised when a record transitions <c>Submitted → Under Review</c>.
/// </summary>
public sealed record RecordUnderReview(
    Guid RecordId,
    Guid ReviewerId,
    DateTime OccurredOn);

/// <summary>
/// Raised when a record transitions <c>Under Review → Verified</c>. The
/// verified record is the authoritative baseline; the verifier differs from
/// the creator (separation of duties).
/// </summary>
public sealed record RecordVerified(
    Guid RecordId,
    Guid VerifiedBy,
    DateTime OccurredOn);

/// <summary>
/// Raised when a record transitions <c>Under Review → Rejected</c>. The
/// rejecting subject differs from the creator (separation of duties).
/// </summary>
public sealed record RecordRejected(
    Guid RecordId,
    Guid RejectedBy,
    DateTime OccurredOn);

/// <summary>
/// Raised when a post-verification correction appends a new superseding
/// version. The prior versions are never mutated.
/// </summary>
public sealed record RecordCorrected(
    Guid RecordId,
    int VersionNumber,
    int? SupersedesVersionNumber,
    Guid CorrectedBy,
    DateTime OccurredOn);

/// <summary>
/// Raised when a verified record transitions to <c>Archived</c>.
/// </summary>
public sealed record RecordArchived(Guid RecordId, DateTime OccurredOn);

/// <summary>
/// Raised when a record transitions to <c>Deactivated</c> (soft-delete).
/// Data is preserved and the transition is reversible by restore. An active
/// hold blocks this unless an administrative override with a reason was used.
/// </summary>
public sealed record RecordDeactivated(Guid RecordId, DateTime OccurredOn);

/// <summary>
/// Raised when a record is restored from <c>Archived</c>/<c>Deactivated</c>
/// back to <c>Verified</c> (the authoritative state).
/// </summary>
public sealed record RecordRestored(Guid RecordId, string Status, DateTime OccurredOn);

/// <summary>
/// Raised when classification/sensitive/retention metadata changes. Carries
/// the classification code string and the sensitive operational gate. No
/// field values and no hold reasons.
/// </summary>
public sealed record RecordClassified(
    Guid RecordId,
    string? ClassificationCode,
    bool IsSensitive,
    DateTime OccurredOn);

/// <summary>
/// Raised when a legal or administrative hold is placed. Intentionally
/// carries no document references — consumers resolve the hold's document
/// scope through the Records API (<c>GET /holds/{id}</c>) rather than
/// expecting those references here (ADR-023). No PII and no hold reason.
/// </summary>
public sealed record RecordHoldPlaced(
    Guid HoldId,
    Guid RecordId,
    string HoldType,
    Guid PlacedBy,
    DateTime OccurredOn);

/// <summary>
/// Raised when a legal or administrative hold is released by a subject
/// different from the placer. Intentionally carries no document references.
/// </summary>
public sealed record RecordHoldReleased(
    Guid HoldId,
    Guid RecordId,
    string HoldType,
    Guid ReleasedBy,
    DateTime OccurredOn);

/// <summary>
/// Raised when a record's retention schedule/period reference changes.
/// Carries only the schedule code and the effective period string; never
/// field values or names.
/// </summary>
public sealed record RecordRetentionChanged(
    Guid RecordId,
    string? RetentionScheduleCode,
    string? RetentionPeriod,
    DateTime OccurredOn);

/// <summary>
/// Raised when a document version is attached to a record as evidence.
/// Carries only the document id, version number and reference type.
/// </summary>
public sealed record RecordEvidenceAttached(
    Guid RecordId,
    Guid DocumentId,
    int VersionNumber,
    string ReferenceType,
    DateTime OccurredOn);

/// <summary>
/// Raised when evidence is removed from a record. Carries only the document
/// id and version number of the removed reference.
/// </summary>
public sealed record RecordEvidenceRemoved(
    Guid RecordId,
    Guid DocumentId,
    int VersionNumber,
    DateTime OccurredOn);

/// <summary>
/// Raised when a record's retention period lapses and the record is flagged
/// for a ratified disposition review. Retention expiry never destroys data
/// through normal operations (ADR-023).
/// </summary>
public sealed record RecordRetentionExpired(
    Guid RecordId,
    string RetentionScheduleCode,
    DateTime ExpiredOn,
    DateTime OccurredOn);
