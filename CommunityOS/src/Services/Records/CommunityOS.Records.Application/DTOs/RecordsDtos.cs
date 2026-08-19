namespace CommunityOS.Records.Application.DTOs;

public sealed record RecordFieldDto(string FieldKey, string Value, bool IsSensitive);

public sealed record RecordVersionDescriptorDto(
    Guid Id,
    int VersionNumber,
    int? SupersedesVersionNumber,
    Guid AppliedBy,
    DateTime AppliedOn,
    string? ChangeReason,
    int FieldCount);

public sealed record RecordVersionDto(
    Guid Id,
    int VersionNumber,
    int? SupersedesVersionNumber,
    Guid AppliedBy,
    DateTime AppliedOn,
    string? ChangeReason,
    IReadOnlyList<RecordFieldDto> Fields);

public sealed record RecordSubjectDto(string SubjectType, Guid SubjectId);

public sealed record RecordClassificationDto(
    string? ClassificationCode,
    bool IsSensitive,
    string? RetentionScheduleCode,
    DateTime? RetentionExpiredOn);

public sealed record RecordEvidenceReferenceDto(
    Guid Id,
    Guid DocumentId,
    int VersionNumber,
    string ReferenceType,
    Guid AttachedBy,
    DateTime AttachedOn);

public sealed record RecordHoldDocumentReferenceDto(Guid DocumentId, int? VersionNumber);

public sealed record RecordHoldDto(
    Guid Id,
    string HoldType,
    string Status,
    Guid RecordId,
    Guid PlacedBy,
    DateTime PlacedOn,
    Guid? ReleasedBy,
    DateTime? ReleasedOn,
    IReadOnlyList<RecordHoldDocumentReferenceDto> DocumentReferences,
    string? Reason);

public sealed record RecordSummaryDto(
    Guid Id,
    string Category,
    string Status,
    string SubjectType,
    Guid? OrganizationUnitId,
    int? CurrentVersionNumber,
    DateTime UpdatedOn);

public sealed record RecordDto(
    Guid Id,
    string Category,
    string Status,
    RecordSubjectDto Subject,
    Guid? OrganizationUnitId,
    IReadOnlyList<Guid> OrganizationScopes,
    RecordClassificationDto Classification,
    RecordVersionDescriptorDto? CurrentVersion,
    IReadOnlyList<RecordEvidenceReferenceDto> Evidence,
    IReadOnlyList<RecordHoldDto> Holds,
    Guid CreatedBy,
    DateTime CreatedOn,
    Guid UpdatedBy,
    DateTime UpdatedOn,
    Guid? VerifiedBy,
    DateTime? VerifiedOn);

public sealed record RetentionRuleDto(
    string Category,
    string RetentionPeriod,
    string StartTrigger,
    string Disposition,
    string? Note,
    string? MaximumPeriod);

public sealed record RetentionScheduleDto(
    string Code,
    string DisplayName,
    string? Description,
    bool IsRetired,
    IReadOnlyList<RetentionRuleDto> Rules,
    Guid CreatedBy,
    DateTime CreatedOn);

public sealed record RecordCategoryDto(
    string Code,
    string DisplayName,
    string? Description,
    bool IsRetired,
    Guid CreatedBy,
    DateTime CreatedOn);

public sealed record OrganizationUnitReferenceDto(Guid OrganizationUnitId, DateTime CreatedOn);