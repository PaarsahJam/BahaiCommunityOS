using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Notifications.Application.Authorization;
using CommunityOS.Notifications.Application.DTOs;
using CommunityOS.Notifications.Application.Permissions;
using CommunityOS.Notifications.Domain.Aggregates;
using CommunityOS.Notifications.Domain.Exceptions;
using CommunityOS.Notifications.Domain.Repositories;
using MediatR;
using static CommunityOS.Notifications.Application.Queries.NotificationQueryHelpers;

namespace CommunityOS.Notifications.Application.Queries;

public sealed record ListInboxQuery(
    Guid ActorId,
    string? TypeCode,
    string? Channel,
    string? Status,
    Guid? OrganizationUnitId,
    int Limit,
    int Offset) : IRequest<IReadOnlyList<NotificationSummaryDto>>;

internal sealed class ListInboxQueryHandler(
    INotificationRepository notifications,
    IAuthorizationEvaluator evaluator)
    : IRequestHandler<ListInboxQuery, IReadOnlyList<NotificationSummaryDto>>
{
    public async Task<IReadOnlyList<NotificationSummaryDto>> Handle(ListInboxQuery query, CancellationToken ct)
    {
        // The inbox is the member-facing read surface: candidates are the
        // notifications where the actor is a recipient (relationship tuple).
        // Fail-closed read filtering at the query boundary: only notifications
        // the caller may read are returned; nothing reveals the existence or
        // count of inaccessible notifications (ADR-025). A notification is
        // readable when the caller is a recipient or holds the read permission
        // at ANY of its organization scopes, and never when the caller lacks
        // the sensitive capability for a sensitive notification.
        var candidates = await notifications.ListInboxCandidatesAsync(
            query.ActorId, query.Limit + 1, query.Offset, ct);

        var requests = new List<AuthorizationRequest>();
        var slices = new List<(Notification Notification, int Count)>();
        foreach (var notification in candidates)
        {
            var contexts = NotificationAuthorization.ContextsFor(notification);
            foreach (var context in contexts)
                requests.Add(new AuthorizationRequest(query.ActorId, NotificationsPermissions.NotificationRead, context));
            slices.Add((notification, contexts.Count));
        }

        var decisions = await evaluator.EvaluateBatchAsync(requests, ct);

        var allowed = new List<Notification>();
        var index = 0;
        foreach (var (notification, count) in slices)
        {
            var slice = decisions.Skip(index).Take(count).ToList();
            index += count;

            if (!slice.Any(d => d.Allowed))
                continue;

            // Sensitive notifications additionally require the sensitive
            // capability at any scope; a recipient without it never sees them.
            if (notification.IsSensitive)
            {
                var sensitiveRequests = NotificationAuthorization.ContextsFor(notification)
                    .Select(context => new AuthorizationRequest(
                        query.ActorId, NotificationsPermissions.NotificationReadSensitive, context))
                    .ToList();
                var sensitiveDecisions = await evaluator.EvaluateBatchAsync(sensitiveRequests, ct);
                if (!sensitiveDecisions.Any(d => d.Allowed))
                    continue;
            }

            allowed.Add(notification);
        }

        return allowed
            .Where(n => query.TypeCode is null ||
                string.Equals(n.TypeCode, query.TypeCode, StringComparison.OrdinalIgnoreCase))
            .Where(n => query.Channel is null ||
                string.Equals(n.Channel.Name, query.Channel, StringComparison.OrdinalIgnoreCase))
            .Where(n => query.Status is null ||
                string.Equals(n.Status.Name, query.Status, StringComparison.OrdinalIgnoreCase))
            .Where(n => query.OrganizationUnitId is null ||
                n.AllOrganizationUnitIds.Contains(query.OrganizationUnitId.Value))
            .OrderByDescending(n => n.CreatedOn)
            .Take(query.Limit)
            .Select(n => n.ToSummaryDto())
            .ToList()
            .AsReadOnly();
    }
}

public sealed record GetNotificationQuery(Guid ActorId, Guid NotificationId) : IRequest<NotificationDto>;

internal sealed class GetNotificationQueryHandler(
    INotificationRepository notifications,
    AuthorizationGuard guard)
    : IRequestHandler<GetNotificationQuery, NotificationDto>
{
    public async Task<NotificationDto> Handle(GetNotificationQuery query, CancellationToken ct)
    {
        var notification = await LoadOrNothingAsync(notifications, query.NotificationId, ct);

        // Recipient (relationship tuple) or scope-level read; sensitive
        // notifications additionally require the sensitive capability. Any
        // failure surfaces as 404 (no enumeration oracle).
        if (!await IsReadableAsync(guard, query.ActorId, notification, sensitive: false, ct))
            throw new NotificationNotFoundException(query.NotificationId);

        return notification.ToDto();
    }
}

public sealed record GetNotificationSensitiveQuery(Guid ActorId, Guid NotificationId)
    : IRequest<NotificationSensitiveFieldsDto>;

internal sealed class GetNotificationSensitiveQueryHandler(
    INotificationRepository notifications,
    AuthorizationGuard guard)
    : IRequestHandler<GetNotificationSensitiveQuery, NotificationSensitiveFieldsDto>
{
    public async Task<NotificationSensitiveFieldsDto> Handle(
        GetNotificationSensitiveQuery query, CancellationToken ct)
    {
        var notification = await LoadOrNothingAsync(notifications, query.NotificationId, ct);

        // Sensitive reads require both the base read capability and the
        // sensitive capability; any failure surfaces as 404 (no oracle).
        if (!await IsReadableAsync(guard, query.ActorId, notification, sensitive: true, ct))
            throw new NotificationNotFoundException(query.NotificationId);

        return notification.ToSensitiveFieldsDto();
    }
}

public sealed record ListNotificationTypesQuery(Guid ActorId)
    : IRequest<IReadOnlyList<NotificationTypeDto>>;

internal sealed class ListNotificationTypesQueryHandler(
    INotificationTypeRepository types,
    AuthorizationGuard guard)
    : IRequestHandler<ListNotificationTypesQuery, IReadOnlyList<NotificationTypeDto>>
{
    public async Task<IReadOnlyList<NotificationTypeDto>> Handle(
        ListNotificationTypesQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, NotificationsPermissions.NotificationTemplateRead,
            new AuthorizationContext(ResourceType: "notification"), ct);

        return (await types.ListAsync(ct))
            .OrderBy(t => t.Code)
            .Select(t => t.ToDto())
            .ToList()
            .AsReadOnly();
    }
}

public sealed record GetNotificationTypeQuery(Guid ActorId, string Code) : IRequest<NotificationTypeDto>;

internal sealed class GetNotificationTypeQueryHandler(
    INotificationTypeRepository types,
    AuthorizationGuard guard)
    : IRequestHandler<GetNotificationTypeQuery, NotificationTypeDto>
{
    public async Task<NotificationTypeDto> Handle(GetNotificationTypeQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, NotificationsPermissions.NotificationTemplateRead,
            new AuthorizationContext(ResourceType: "notification"), ct);

        var type = await types.GetByCodeAsync(query.Code, ct)
            ?? throw new NotificationTypeNotFoundException(query.Code);

        return type.ToDto();
    }
}

public sealed record GetMemberPreferencesQuery(Guid ActorId) : IRequest<MemberNotificationPreferencesDto>;

internal sealed class GetMemberPreferencesQueryHandler(
    INotificationPreferenceRepository preferences,
    AuthorizationGuard guard)
    : IRequestHandler<GetMemberPreferencesQuery, MemberNotificationPreferencesDto>
{
    public async Task<MemberNotificationPreferencesDto> Handle(
        GetMemberPreferencesQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, NotificationsPermissions.NotificationPreferenceManage,
            new AuthorizationContext(ResourceType: "notification.preference"), ct);

        return new MemberNotificationPreferencesDto(query.ActorId,
            (await preferences.ListByMemberAsync(query.ActorId, ct)).ToRulesDto());
    }
}

internal static class NotificationQueryHelpers
{
    public static async Task<Notification> LoadOrNothingAsync(
        INotificationRepository notifications, Guid notificationId, CancellationToken ct) =>
        await notifications.GetByIdAsync(notificationId, ct)
        ?? throw new NotificationNotFoundException(notificationId);

    /// <summary>
    /// Readability for the notification: recipient (relationship tuple) or
    /// scope-level read. When <paramref name="sensitive"/> is true the base read
    /// and the sensitive capability are both required.
    /// </summary>
    public static async Task<bool> IsReadableAsync(
        AuthorizationGuard guard,
        Guid actorId,
        Notification notification,
        bool sensitive,
        CancellationToken ct)
    {
        var readable = notification.IsRecipient(actorId) ||
            await NotificationAuthorization.HasForNotificationAsync(
                guard, actorId, NotificationsPermissions.NotificationRead, notification, ct);

        if (!readable)
            return false;

        if (sensitive || notification.IsSensitive)
            return await NotificationAuthorization.HasForNotificationAsync(
                guard, actorId, NotificationsPermissions.NotificationReadSensitive, notification, ct);

        return true;
    }
}