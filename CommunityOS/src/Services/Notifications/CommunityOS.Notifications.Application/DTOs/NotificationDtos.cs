namespace CommunityOS.Notifications.Application.DTOs;

public sealed record NotificationSummaryDto(
    Guid Id,
    string TypeCode,
    string Channel,
    string Status,
    Guid? SourceId,
    Guid? OrganizationUnitId,
    DateTime CreatedOn);

public sealed record NotificationRecipientDto(
    Guid MemberId,
    string Channel,
    string Status,
    DateTime? DeliveredAt,
    DateTime? ReadAt,
    string? FailureReason);

public sealed record NotificationDto(
    Guid Id,
    string TypeCode,
    string Channel,
    string Status,
    string? SourceType,
    Guid? SourceId,
    Guid? OrganizationUnitId,
    IReadOnlyList<Guid> AdditionalScopes,
    IReadOnlyList<Guid> RecipientIds,
    DateTime? ScheduledFor,
    bool IsSensitive,
    Guid CreatedBy,
    DateTime CreatedOn,
    DateTime? DispatchedOn);

/// <summary>
/// Sensitive notification fields — per-recipient delivery diagnostics (failure
/// reasons, full distribution), returned only under
/// <c>notifications.notification.read.sensitive</c>. Subject/body copy is never
/// exported in integration events or logs.
/// </summary>
public sealed record NotificationSensitiveFieldsDto(
    Guid NotificationId,
    IReadOnlyList<NotificationRecipientDto> Distribution);

public sealed record NotificationTypeDto(
    string Code,
    string DisplayName,
    string DefaultChannel,
    string SubjectTemplate,
    string BodyTemplate,
    bool IsSensitive,
    bool IsActive,
    Guid CreatedBy,
    DateTime CreatedOn,
    Guid UpdatedBy,
    DateTime UpdatedOn);

/// <summary>
/// Member-safe notification summary for the authenticated recipient
/// (relationship-granted, fail-closed). Delivers notification identity, type,
/// channel, lifecycle status, creation timestamp, member-visible content and
/// recipient-specific read state. Exposes no recipient distribution, no
/// source/scope metadata, and never a sensitive notification (ADR-027 member
/// read contract).
/// </summary>
public sealed record MemberNotificationSummaryDto(
    Guid Id,
    string TypeCode,
    string Channel,
    string Status,
    string Title,
    string Body,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedOn);

/// <summary>
/// Recipient-scoped unread count for the authenticated member (delivered, not
/// read). Carries no notification details — count only.
/// </summary>
public sealed record MemberUnreadCountDto(int Count);

public sealed record NotificationPreferenceRuleDto(
    string TypeCode,
    IReadOnlyList<string> Channels,
    bool Enabled);

public sealed record MemberNotificationPreferencesDto(
    Guid MemberId,
    IReadOnlyList<NotificationPreferenceRuleDto> Rules);