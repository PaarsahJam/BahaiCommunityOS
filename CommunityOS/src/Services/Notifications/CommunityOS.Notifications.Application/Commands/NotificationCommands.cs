using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Notifications.Application.Authorization;
using CommunityOS.Notifications.Application.DTOs;
using CommunityOS.Notifications.Application.Logging;
using CommunityOS.Notifications.Application.Permissions;
using CommunityOS.Notifications.Application.Services;
using CommunityOS.Notifications.Domain.Aggregates;
using CommunityOS.Notifications.Domain.Entities;
using CommunityOS.Notifications.Domain.Exceptions;
using CommunityOS.Notifications.Domain.Repositories;
using CommunityOS.Notifications.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using static CommunityOS.Notifications.Application.Commands.NotificationCommandHelpers;
using NotificationsEventsPublisher = CommunityOS.Notifications.Application.Pipeline.DomainEventPublisher;

namespace CommunityOS.Notifications.Application.Commands;

public sealed record CreateNotificationCommand(
    Guid ActorId,
    string TypeCode,
    string Channel,
    string? SourceType,
    Guid? SourceId,
    Guid? OrganizationUnitId,
    IReadOnlyList<Guid> AdditionalScopes,
    IReadOnlyList<Guid> RecipientIds,
    DateTime? ScheduledFor,
    bool IsSensitive,
    string Subject,
    string Body) : IRequest<NotificationDto>;

internal sealed class CreateNotificationCommandHandler(
    INotificationRepository notifications,
    INotificationTypeRepository types,
    IOrganizationUnitReferenceRepository units,
    AuthorizationGuard guard,
    IMediator mediator,
    ILogger<CreateNotificationCommandHandler> logger)
    : IRequestHandler<CreateNotificationCommand, NotificationDto>
{
    public async Task<NotificationDto> Handle(CreateNotificationCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, NotificationsPermissions.NotificationCreate,
            cmd.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "notification")
                : new AuthorizationContext(ResourceType: "notification"), ct);

        var type = await types.GetByCodeAsync(cmd.TypeCode, ct)
            ?? throw new InvalidNotificationTypeReferenceException(cmd.TypeCode);

        if (!type.IsActive)
            throw new InvalidNotificationTypeReferenceException(cmd.TypeCode);

        await EnsureUnitExistsAsync(units, cmd.OrganizationUnitId, ct);
        foreach (var scope in cmd.AdditionalScopes ?? [])
            await EnsureUnitExistsAsync(units, scope, ct);

        var channel = NotificationChannel.FromName(cmd.Channel);

        // Creation is idempotent per (type, source type, source id, channel): a
        // second create for the same source fact returns the existing
        // notification (200). Free-standing general notifications (no source)
        // are always new (docs/api/notifications.md).
        if (cmd.SourceType is not null && cmd.SourceId is { } sourceId)
        {
            var existing = await notifications.GetBySourceAsync(cmd.TypeCode, cmd.SourceType, sourceId, ct);
            if (existing is not null)
                return existing.ToDto();
        }

        var notification = Notification.Create(
            cmd.TypeCode,
            channel,
            cmd.SourceType,
            cmd.SourceId,
            MessageTemplate.Create(cmd.Subject, cmd.Body),
            cmd.OrganizationUnitId,
            cmd.AdditionalScopes ?? [],
            cmd.ScheduledFor,
            cmd.IsSensitive,
            cmd.RecipientIds ?? [],
            cmd.ActorId,
            DateTime.UtcNow);

        // The notification is created in Draft; it moves to Queued and is
        // dispatched by the dispatch worker (or by an explicit dispatch call).
        notification.Queue();

        // Outbox: domain events are published before the single transaction
        // commits so the forwarded integration event (if any) and the
        // notification row are committed atomically (ADR-015, ratified).
        await NotificationsEventsPublisher.PublishAsync(notification, mediator, ct);
        var persisted = await notifications.AddIfAbsentAsync(notification, ct);

        if (persisted.Id != notification.Id)
        {
            // A concurrent create won the race; the unique filtered index
            // rejected our insert and the existing notification was returned.
            logger.NotificationCreated(persisted.Id, persisted.TypeCode);
            return persisted.ToDto();
        }

        logger.NotificationCreated(notification.Id, notification.TypeCode);
        return notification.ToDto();
    }
}

public sealed record DispatchNotificationCommand(
    Guid ActorId,
    Guid NotificationId,
    bool AdminOverride = false) : IRequest<NotificationDto>;

internal sealed class DispatchNotificationCommandHandler(
    INotificationRepository notifications,
    AuthorizationGuard guard,
    NotificationDispatchService dispatchService,
    ILogger<DispatchNotificationCommandHandler> logger)
    : IRequestHandler<DispatchNotificationCommand, NotificationDto>
{
    public async Task<NotificationDto> Handle(DispatchNotificationCommand cmd, CancellationToken ct)
    {
        var notification = await LoadForMutationAsync(notifications, cmd.NotificationId, ct);
        await NotificationAuthorization.RequireForNotificationAsync(
            guard, cmd.ActorId, NotificationsPermissions.NotificationSend, notification, ct);

        if (cmd.AdminOverride)
        {
            await NotificationAuthorization.RequireForNotificationAsync(
                guard, cmd.ActorId, NotificationsPermissions.NotificationAdmin, notification, ct);
            logger.AdminOverrideApplied(notification.Id, "dispatch");
        }

        await dispatchService.DispatchAsync(notification, ct);
        await notifications.UpdateAsync(notification, ct);

        return notification.ToDto();
    }
}

public sealed record MarkNotificationReadCommand(
    Guid ActorId,
    Guid NotificationId,
    Guid MemberId) : IRequest<NotificationDto>;

internal sealed class MarkNotificationReadCommandHandler(
    INotificationRepository notifications,
    IMediator mediator)
    : IRequestHandler<MarkNotificationReadCommand, NotificationDto>
{
    public async Task<NotificationDto> Handle(MarkNotificationReadCommand cmd, CancellationToken ct)
    {
        var notification = await LoadForMutationAsync(notifications, cmd.NotificationId, ct);

        // The actor must be the recipient (relationship tuple); an unauthorized
        // caller receives 404 — no oracle.
        if (!notification.IsRecipient(cmd.MemberId) || cmd.MemberId != cmd.ActorId)
            throw new NotificationNotFoundException(cmd.NotificationId);

        notification.MarkRead(cmd.MemberId, DateTime.UtcNow);

        // Read events are domain-only (never exported); they are still
        // dispatched through the mediator for in-process observers before the
        // single transaction commits.
        await NotificationsEventsPublisher.PublishAsync(notification, mediator, ct);
        await notifications.UpdateAsync(notification, ct);

        return notification.ToDto();
    }
}

public sealed record CreateNotificationTypeCommand(
    Guid ActorId,
    string Code,
    string DisplayName,
    string DefaultChannel,
    string SubjectTemplate,
    string BodyTemplate,
    bool IsSensitive) : IRequest<NotificationTypeDto>;

internal sealed class CreateNotificationTypeCommandHandler(
    INotificationTypeRepository types,
    AuthorizationGuard guard,
    ILogger<CreateNotificationTypeCommandHandler> logger)
    : IRequestHandler<CreateNotificationTypeCommand, NotificationTypeDto>
{
    public async Task<NotificationTypeDto> Handle(CreateNotificationTypeCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, NotificationsPermissions.NotificationTypeManage,
            new AuthorizationContext(ResourceType: "notification"), ct);

        if (await types.GetByCodeAsync(cmd.Code, ct) is not null)
            throw new DuplicateNotificationTypeException(cmd.Code);

        var type = NotificationType.Create(
            cmd.Code, cmd.DisplayName, NotificationChannel.FromName(cmd.DefaultChannel),
            cmd.SubjectTemplate, cmd.BodyTemplate, cmd.IsSensitive, cmd.ActorId, DateTime.UtcNow);

        await types.AddAsync(type, ct);
        logger.NotificationTypeCreated(type.Code);
        return type.ToDto();
    }
}

public sealed record UpdateNotificationTypeCommand(
    Guid ActorId,
    string Code,
    string DisplayName,
    string DefaultChannel,
    string SubjectTemplate,
    string BodyTemplate,
    bool IsSensitive) : IRequest<NotificationTypeDto>;

internal sealed class UpdateNotificationTypeCommandHandler(
    INotificationTypeRepository types,
    AuthorizationGuard guard)
    : IRequestHandler<UpdateNotificationTypeCommand, NotificationTypeDto>
{
    public async Task<NotificationTypeDto> Handle(UpdateNotificationTypeCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, NotificationsPermissions.NotificationTypeManage,
            new AuthorizationContext(ResourceType: "notification"), ct);

        var type = await types.GetByCodeAsync(cmd.Code, ct)
            ?? throw new NotificationTypeNotFoundException(cmd.Code);

        type.Update(
            cmd.DisplayName, NotificationChannel.FromName(cmd.DefaultChannel),
            cmd.SubjectTemplate, cmd.BodyTemplate, cmd.IsSensitive, cmd.ActorId, DateTime.UtcNow);

        await types.UpdateAsync(type, ct);
        return type.ToDto();
    }
}

public sealed record RetireNotificationTypeCommand(Guid ActorId, string Code)
    : IRequest<NotificationTypeDto>;

internal sealed class RetireNotificationTypeCommandHandler(
    INotificationTypeRepository types,
    AuthorizationGuard guard,
    ILogger<RetireNotificationTypeCommandHandler> logger)
    : IRequestHandler<RetireNotificationTypeCommand, NotificationTypeDto>
{
    public async Task<NotificationTypeDto> Handle(RetireNotificationTypeCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, NotificationsPermissions.NotificationTypeManage,
            new AuthorizationContext(ResourceType: "notification"), ct);

        var type = await types.GetByCodeAsync(cmd.Code, ct)
            ?? throw new NotificationTypeNotFoundException(cmd.Code);

        if (await types.AnyNotificationReferencesAsync(cmd.Code, ct))
            throw new NotificationTypeInUseException(cmd.Code);

        type.Retire(cmd.ActorId, DateTime.UtcNow);
        await types.UpdateAsync(type, ct);
        logger.NotificationTypeRetired(type.Code);
        return type.ToDto();
    }
}

public sealed record UpdateMemberPreferencesCommand(
    Guid ActorId,
    IReadOnlyList<NotificationPreferenceRuleDto> Rules) : IRequest<MemberNotificationPreferencesDto>;

internal sealed class UpdateMemberPreferencesCommandHandler(
    INotificationPreferenceRepository preferences,
    INotificationTypeRepository types,
    AuthorizationGuard guard)
    : IRequestHandler<UpdateMemberPreferencesCommand, MemberNotificationPreferencesDto>
{
    public async Task<MemberNotificationPreferencesDto> Handle(UpdateMemberPreferencesCommand cmd, CancellationToken ct)
    {
        // Self-managed: the member manages only their own preferences.
        await guard.RequireAsync(cmd.ActorId, NotificationsPermissions.NotificationPreferenceManage,
            new AuthorizationContext(ResourceType: "notification.preference"), ct);

        foreach (var rule in cmd.Rules ?? [])
        {
            var type = await types.GetByCodeAsync(rule.TypeCode, ct)
                ?? throw new InvalidNotificationTypeReferenceException(rule.TypeCode);

            if (!type.IsActive)
                throw new InvalidNotificationTypeReferenceException(rule.TypeCode);

            foreach (var channelName in rule.Channels)
            {
                var channel = NotificationChannel.FromName(channelName);
                var existing = await preferences.GetAsync(cmd.ActorId, rule.TypeCode, channel.Id, ct);
                if (existing is null)
                {
                    await preferences.AddAsync(
                        NotificationPreference.Create(cmd.ActorId, rule.TypeCode, channel, rule.Enabled), ct);
                }
                else
                {
                    existing.SetEnabled(rule.Enabled);
                    await preferences.UpdateAsync(existing, ct);
                }
            }
        }

        return new MemberNotificationPreferencesDto(cmd.ActorId,
            (await preferences.ListByMemberAsync(cmd.ActorId, ct)).ToRulesDto());
    }
}

internal static class NotificationCommandHelpers
{
    /// <summary>
    /// Loads the notification for a guarded mutation. Ordering guarantees that
    /// authorization is evaluated before any persistence side effect.
    /// </summary>
    public static async Task<Notification> LoadForMutationAsync(
        INotificationRepository notifications,
        Guid id,
        CancellationToken ct) =>
        await notifications.GetByIdAsync(id, ct) ?? throw new NotificationNotFoundException(id);

    internal static async Task EnsureUnitExistsAsync(
        IOrganizationUnitReferenceRepository units, Guid? unitId, CancellationToken ct)
    {
        if (unitId is null)
            return;

        if (await units.GetByOrganizationUnitIdAsync(unitId.Value, ct) is null)
            throw new InvalidScopeReferenceException(unitId.Value);
    }
}