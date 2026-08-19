namespace CommunityOS.Records.Domain.Exceptions;

/// <summary>404 — record not found or not readable (indistinguishable).</summary>
public sealed class RecordNotFoundException(Guid recordId)
    : Exception($"Record '{recordId}' was not found.");

/// <summary>404 — version not found.</summary>
public sealed class RecordVersionNotFoundException(Guid recordId, int versionNumber)
    : Exception($"Version {versionNumber} of record '{recordId}' was not found.");

/// <summary>404 — hold not found.</summary>
public sealed class RecordHoldNotFoundException(Guid holdId)
    : Exception($"Hold '{holdId}' was not found.");

/// <summary>404 — evidence reference not found.</summary>
public sealed class RecordEvidenceNotFoundException(Guid evidenceId)
    : Exception($"Evidence reference '{evidenceId}' was not found.");

/// <summary>404 — retention schedule not found.</summary>
public sealed class RetentionScheduleNotFoundException(string code)
    : Exception($"Retention schedule '{code}' was not found.");

/// <summary>404 — category not found.</summary>
public sealed class RecordCategoryNotFoundException(string code)
    : Exception($"Record category '{code}' was not found.");

/// <summary>400 — a request references a category that does not exist.</summary>
public sealed class InvalidRecordCategoryReferenceException(string code)
    : Exception($"Record category '{code}' is not a known category reference.");

/// <summary>409 — a category with the same code already exists.</summary>
public sealed class DuplicateRecordCategoryException(string code)
    : Exception($"Record category '{code}' already exists.");

/// <summary>409 — a retention schedule with the same code already exists.</summary>
public sealed class DuplicateRetentionScheduleException(string code)
    : Exception($"Retention schedule '{code}' already exists.");

/// <summary>400 — a hold type is not one of the ratified <c>legal</c>|<c>administrative</c>.</summary>
public sealed class InvalidRecordHoldTypeException(string holdType)
    : Exception($"Hold type '{holdType}' is not recognized (legal or administrative).");

/// <summary>400 — lifecycle transition is not permitted.</summary>
public sealed class InvalidRecordTransitionException(Guid recordId, string from, string to)
    : Exception($"Record '{recordId}' cannot transition from '{from}' to '{to}'.");

/// <summary>409 — field update attempted on a Verified record (correction required).</summary>
public sealed class VerifiedRecordFieldUpdateException(Guid recordId)
    : Exception($"Record '{recordId}' is verified; field changes require the correct operation, never an in-place update.");

/// <summary>409 — the creator cannot verify (or move into/out of Under Review) the same record.</summary>
public sealed class CreatorVerificationConflictException(Guid recordId)
    : Exception($"Record '{recordId}' cannot be verified by its creator (separation of duties).");

/// <summary>409 — the placer of a hold cannot release that same hold.</summary>
public sealed class HoldReleaseByPlacerException(Guid holdId)
    : Exception($"Hold '{holdId}' cannot be released by the subject that placed it.");

/// <summary>409 — deactivation is blocked by an active legal/administrative hold.</summary>
public sealed class HeldRecordDeactivationException(Guid recordId)
    : Exception($"Record '{recordId}' cannot be deactivated while an active legal or administrative hold is present.");

/// <summary>409 — duplicate evidence reference row.</summary>
public sealed class DuplicateRecordEvidenceException(Guid recordId)
    : Exception($"An evidence reference already exists for record '{recordId}' with the same document, version and type.");

/// <summary>400 — organization-scope reference is not a known unit reference.</summary>
public sealed class InvalidRecordScopeException(Guid organizationUnitId)
    : Exception($"Organization unit scope '{organizationUnitId}' is not a known unit reference.");

/// <summary>409 — a retention schedule that is in use cannot be retired.</summary>
public sealed class RetentionScheduleInUseException(string code)
    : Exception($"Retention schedule '{code}' is in use by one or more records and cannot be retired.");

/// <summary>400 — a retention rule must carry a valid ISO-8601 period.</summary>
public sealed class InvalidRetentionPeriodException(string period)
    : Exception($"Retention period '{period}' is not a valid ISO-8601 duration.");

/// <summary>400 — a change reason is required for a post-verification correction.</summary>
public sealed class CorrectionChangeReasonRequiredException(Guid recordId)
    : Exception($"A change reason is required to correct record '{recordId}'.");

/// <summary>400 — an administrative override requires a reason.</summary>
public sealed class AdminOverrideReasonRequiredException(Guid recordId)
    : Exception($"An administrative override for record '{recordId}' requires a reason.");

/// <summary>400 — the record's subject type is not recognized.</summary>
public sealed class InvalidRecordSubjectTypeException(string subjectType)
    : Exception($"Subject type '{subjectType}' is not recognized.");