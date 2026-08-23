using CommunityOS.Correspondence.Domain;

namespace CommunityOS.Correspondence.Application;

/// <summary>Deterministic query filters for list and export (ADR-028).
/// There is deliberately NO subject or body search.</summary>
public sealed record LetterQueryFilters(
    string? Status,
    string? Category,
    Guid? OrganizationUnitId,
    DateTime? SubmittedFrom,
    DateTime? SubmittedTo,
    string? Reference);

// ---- Store projections -----------------------------------------------------

/// <summary>Metadata-only summary row: subjects/bodies/display lines never
/// appear in lists or exports (ADR-028 privacy posture).</summary>
public sealed record LetterSummaryRow(
    Guid Id,
    Guid OrganizationUnitId,
    string CategoryCode,
    int? LetterYear,
    int? LetterSequence,
    string Status,
    string Sensitivity,
    bool IsHeld,
    int RecipientCount,
    int PersonRecipientCount,
    int UnitRecipientCount,
    int ExternalRecipientCount,
    Guid? SubjectPersonId,
    DateTime CreatedOn,
    DateTime? SubmittedOn)
{
    /// <summary>Ratified display composition <c>{year}-{sequence:D5}</c>.</summary>
    public string? Reference => LetterYear is { } y && LetterSequence is { } s ? $"{y}-{s:D5}" : null;
}

public sealed record LetterRecipientRow(
    Guid Id, string Kind, Guid? PersonId, Guid? UnitId, string? DisplayLine);

public sealed record LetterDocumentLinkRow(Guid DocumentId, int VersionNumber, string ContentHash, DateTime MaterializedOn);

public sealed record LetterAttachmentRow(Guid Id, Guid DocumentId, string ReferenceType, DateTime AddedOn);

public sealed record LetterDetailRow(
    Guid Id,
    Guid OrganizationUnitId,
    string CategoryCode,
    string Subject,
    string Body,
    string Sensitivity,
    string Status,
    int Revision,
    int? LetterYear,
    int? LetterSequence,
    Guid? TemplateId,
    string? TemplateCode,
    Guid? RelatedLetterId,
    Guid? SubjectPersonId,
    Guid CreatedBy,
    DateTime CreatedOn,
    DateTime UpdatedOn,
    Guid? SubmittedBy,
    DateTime? SubmittedOn,
    DateTime? MaterializedOn,
    DateTime? DispatchedOn,
    DateTime? DeliveredOn,
    string? DeliveryFailureReasonCode,
    Guid? CancelledBy,
    DateTime? CancelledOn,
    string? CancellationReasonCode,
    string RetentionClass,
    DateTime? RetentionExpiresOn,
    bool IsHeld,
    IReadOnlyList<LetterRecipientRow> Recipients,
    IReadOnlyList<LetterDocumentLinkRow> DocumentLinks,
    IReadOnlyList<LetterAttachmentRow> Attachments)
{
    public string? Reference => LetterYear is { } y && LetterSequence is { } s ? $"{y}-{s:D5}" : null;
}

public sealed record HistoryRow(
    Guid Id,
    LetterStatus FromStatus,
    LetterStatus ToStatus,
    HistoryCause Cause,
    Guid? ActorId,
    string? ReasonCode,
    DateTime OccurredOn);

public sealed record HoldRow(
    Guid Id, Guid LetterId, string HoldType, string ReasonCode,
    Guid PlacedBy, DateTime PlacedOn, Guid? ReleasedBy, DateTime? ReleasedOn);

public sealed record TemplateRow(
    Guid Id, string Code, string Title, string CategoryCode,
    DateTime CreatedOn, DateTime UpdatedOn, bool IsActive);

public sealed record StuckSubmissionRow(Guid Id, Guid OrganizationUnitId, DateTime SubmittedOn);
