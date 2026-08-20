using CommunityOS.Notifications.Application.DTOs;
using CommunityOS.Notifications.Domain.Aggregates;
using CommunityOS.Notifications.Domain.Entities;

namespace CommunityOS.Notifications.Application;

internal static class NotificationMappingExtensions
{
    internal static NotificationSummaryDto ToSummaryDto(this Notification n) =>
        new(n.Id,
            n.TypeCode,
            n.Channel.Name,
            n.Status.Name,
            n.SourceId,
            n.OrganizationUnitId,
            n.CreatedOn);

    internal static NotificationDto ToDto(this Notification n) =>
        new(n.Id,
            n.TypeCode,
            n.Channel.Name,
            n.Status.Name,
            n.SourceType,
            n.SourceId,
            n.OrganizationUnitId,
            n.AllOrganizationUnitIds.Except(
                    n.OrganizationUnitId is { } organizationUnitId
                        ? new[] { organizationUnitId }
                        : Array.Empty<Guid>())
                .ToArray()
                .AsReadOnly(),
            n.Recipients.Select(r => r.MemberId).ToArray().AsReadOnly(),
            n.ScheduledFor,
            n.IsSensitive,
            n.CreatedBy,
            n.CreatedOn,
            n.DispatchedOn);

    internal static NotificationSensitiveFieldsDto ToSensitiveFieldsDto(this Notification n) =>
        new(n.Id,
            n.Recipients
                .OrderBy(r => r.MemberId)
                .Select(r => r.ToDto())
                .ToArray()
                .AsReadOnly());

    internal static NotificationRecipientDto ToDto(this NotificationRecipient r) =>
        new(r.MemberId, r.Channel.Name, r.Status.Name, r.DeliveredAt, r.ReadAt, r.FailureReason);

    internal static NotificationTypeDto ToDto(this NotificationType t) =>
        new(t.Code,
            t.DisplayName,
            t.DefaultChannel.Name,
            t.SubjectTemplate,
            t.BodyTemplate,
            t.IsSensitive,
            t.IsActive,
            t.CreatedBy,
            t.CreatedOn,
            t.UpdatedBy,
            t.UpdatedOn);

    internal static IReadOnlyList<NotificationPreferenceRuleDto> ToRulesDto(
        this IReadOnlyList<NotificationPreference> preferences) =>
        preferences
            .GroupBy(p => p.TypeCode)
            .Select(group => new NotificationPreferenceRuleDto(
                group.Key,
                group.Select(p => p.Channel.Name).OrderBy(c => c).ToArray().AsReadOnly(),
                group.All(p => p.Enabled)))
            .OrderBy(r => r.TypeCode)
            .ToArray()
            .AsReadOnly();
}