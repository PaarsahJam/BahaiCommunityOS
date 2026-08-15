namespace CommunityOS.Documents.Application.DTOs;

public sealed record OwnerReferenceDto(string OwnerType, Guid OwnerId);

public sealed record DocumentClassificationDto(
    string? ClassificationCode,
    bool IsSensitive,
    string? RetentionCategory,
    string? LegalHoldReference,
    string? AdministrativeHoldReference);

public sealed record DocumentVersionDto(
    Guid Id,
    int VersionNumber,
    string MimeType,
    long SizeBytes,
    string FileName,
    string ContentHash,
    Guid UploadedBy,
    DateTime UploadedOn,
    string Source,
    string ScanStatus);

public sealed record DocumentReferenceDto(
    Guid Id,
    string SourceContext,
    Guid SourceEntityId,
    string ReferenceType,
    Guid CreatedBy,
    DateTime CreatedOn);

public sealed record DocumentSummaryDto(
    Guid Id,
    string Title,
    string Status,
    string? ClassificationCode,
    bool IsSensitive,
    Guid? OrganizationUnitId,
    int? CurrentVersionNumber,
    DateTime UpdatedOn);

public sealed record DocumentDto(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    OwnerReferenceDto? Owner,
    Guid? OrganizationUnitId,
    IReadOnlyList<Guid> OrganizationScopes,
    DocumentClassificationDto Classification,
    DocumentVersionDto? CurrentVersion,
    Guid CreatedBy,
    DateTime CreatedOn,
    Guid UpdatedBy,
    DateTime UpdatedOn);

public sealed record OrganizationUnitReferenceDto(
    Guid OrganizationUnitId,
    Guid OrganizationId,
    string Name,
    string UnitType,
    Guid? ParentId,
    DateTime LastSeenOn);

/// <summary>
/// Streamed content result. Binary content never appears in a JSON payload —
/// the controller streams this stream to the HTTP response.
/// </summary>
public sealed record DocumentContentDto(
    Stream Content,
    string MimeType,
    string FileName,
    long SizeBytes,
    Guid VersionId);