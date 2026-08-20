using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Notifications.Application.Commands;
using CommunityOS.Notifications.Domain.Exceptions;
using CommunityOS.Notifications.Domain.ValueObjects;
using NSubstitute;

namespace CommunityOS.Notifications.Tests.Application;

/// <summary>
/// Command-level behavior for the notification pipeline (ADR-025): idempotent
/// create per source fact, guarded mutations, mark-read without an existence
/// oracle, and the type-catalog commands (create/update/retire with the in-use
/// guard).
/// </summary>
public class NotificationCommandTests
{
    private static readonly Guid Actor = ApplicationTestData.Actor;
    private static readonly Guid Recipient = ApplicationTestData.Recipient;

    // --- Create ---

    [Fact]
    public async Task Create_requires_notification_create_permission()
    {
        var h = new ApplicationHarness();
        h.DenyAll();
        h.Repos.Types.GetByCodeAsync("general", Arg.Any<CancellationToken>())
            .Returns(ApplicationHarness.CreateType());

        var act = () => h.Sender.Send(new CreateNotificationCommand(
            Actor, "general", "InApp", null, null, null, [], [Recipient], null, false,
            "Subject", "Body"));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
    }

    [Fact]
    public async Task Create_returns_existing_notification_for_the_same_source_fact()
    {
        var h = new ApplicationHarness();
        h.Allow(CommunityOS.Notifications.Application.Permissions.NotificationsPermissions.NotificationCreate);
        var existing = ApplicationHarness.CreateQueuedNotification(sourceId: Guid.NewGuid());
        h.Repos.Notifications.GetBySourceAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(existing);
        h.Repos.Types.GetByCodeAsync("general", Arg.Any<CancellationToken>())
            .Returns(ApplicationHarness.CreateType());

        var dto = await h.Sender.Send(new CreateNotificationCommand(
            Actor, "general", "InApp", "workflow-task", existing.SourceId, null, [], [Recipient], null, false,
            "Subject", "Body"));

        dto.Id.Should().Be(existing.Id);
        await h.Repos.Notifications.DidNotReceive().AddIfAbsentAsync(
            Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_persists_a_queued_notification_when_absent()
    {
        var h = new ApplicationHarness();
        h.Allow(CommunityOS.Notifications.Application.Permissions.NotificationsPermissions.NotificationCreate);
        h.Repos.Types.GetByCodeAsync("general", Arg.Any<CancellationToken>())
            .Returns(ApplicationHarness.CreateType());
        h.Repos.Notifications.AddIfAbsentAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Notification>());

        var dto = await h.Sender.Send(new CreateNotificationCommand(
            Actor, "general", "InApp", null, null, null, [], [Recipient], null, false,
            "Subject", "Body"));

        dto.Status.Should().Be("Queued");
        dto.RecipientIds.Should().Equal(Recipient);
        await h.Repos.Notifications.Received(1).AddIfAbsentAsync(
            Arg.Is<Notification>(n => n.TypeCode == "general"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_rejects_unknown_or_retired_type()
    {
        var h = new ApplicationHarness();
        h.Allow(CommunityOS.Notifications.Application.Permissions.NotificationsPermissions.NotificationCreate);
        h.Repos.Types.GetByCodeAsync("general", Arg.Any<CancellationToken>())
            .Returns((NotificationType?)null);

        var act = () => h.Sender.Send(new CreateNotificationCommand(
            Actor, "general", "InApp", null, null, null, [], [Recipient], null, false,
            "Subject", "Body"));

        await act.Should().ThrowAsync<InvalidNotificationTypeReferenceException>();
    }

    // --- Dispatch ---

    [Fact]
    public async Task Dispatch_requires_send_at_any_scope_of_the_notification()
    {
        var h = new ApplicationHarness();
        h.DenyAll();
        var notification = ApplicationHarness.CreateQueuedNotification();
        h.Repos.Notifications.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>())
            .Returns(notification);

        var act = () => h.Sender.Send(new DispatchNotificationCommand(Actor, notification.Id));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
    }

    [Fact]
    public async Task Dispatch_with_admin_override_requires_admin_permission()
    {
        var h = new ApplicationHarness();
        h.Allow(CommunityOS.Notifications.Application.Permissions.NotificationsPermissions.NotificationSend);
        var notification = ApplicationHarness.CreateQueuedNotification();
        h.Repos.Notifications.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>())
            .Returns(notification);

        var act = () => h.Sender.Send(new DispatchNotificationCommand(Actor, notification.Id, AdminOverride: true));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
    }

    [Fact]
    public async Task Dispatch_delivers_inapp_and_marks_notification_dispatched()
    {
        var h = new ApplicationHarness();
        h.Allow(CommunityOS.Notifications.Application.Permissions.NotificationsPermissions.NotificationSend);
        var notification = ApplicationHarness.CreateQueuedNotification(
            recipients: [Recipient], channel: NotificationChannel.InApp);
        h.Repos.Notifications.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>())
            .Returns(notification);

        var dto = await h.Sender.Send(new DispatchNotificationCommand(Actor, notification.Id));

        dto.Status.Should().Be("Dispatched");
        dto.DispatchedOn.Should().NotBeNull();
        await h.Repos.Notifications.Received(1).UpdateAsync(notification, Arg.Any<CancellationToken>());
    }

    // --- Mark read ---

    [Fact]
    public async Task MarkRead_by_non_recipient_or_other_actor_is_404_not_found()
    {
        var h = new ApplicationHarness();
        var other = Guid.NewGuid();
        var notification = ApplicationHarness.CreateQueuedNotification(
            recipients: [Recipient], channel: NotificationChannel.InApp);
        notification.Dispatch(DateTime.UtcNow);
        h.Repos.Notifications.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>())
            .Returns(notification);

        var act = () => h.Sender.Send(new MarkNotificationReadCommand(other, notification.Id, Recipient));

        await act.Should().ThrowAsync<NotificationNotFoundException>();
    }

    [Fact]
    public async Task MarkRead_by_the_recipient_transitions_to_read()
    {
        var h = new ApplicationHarness();
        var notification = ApplicationHarness.CreateQueuedNotification(
            recipients: [Recipient], channel: NotificationChannel.InApp);
        notification.Dispatch(DateTime.UtcNow);
        h.Repos.Notifications.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>())
            .Returns(notification);

        var dto = await h.Sender.Send(new MarkNotificationReadCommand(Recipient, notification.Id, Recipient));

        dto.Status.Should().Be("Dispatched");
        await h.Repos.Notifications.Received(1).UpdateAsync(notification, Arg.Any<CancellationToken>());
    }

    // --- Type catalog ---

    [Fact]
    public async Task CreateType_requires_type_manage_and_rejects_duplicates()
    {
        var h = new ApplicationHarness();
        h.Allow(CommunityOS.Notifications.Application.Permissions.NotificationsPermissions.NotificationTypeManage);
        h.Repos.Types.GetByCodeAsync("custom", Arg.Any<CancellationToken>())
            .Returns(ApplicationHarness.CreateType("custom"));

        var act = () => h.Sender.Send(new CreateNotificationTypeCommand(
            Actor, "custom", "Custom", "InApp", "Subject", "Body", false));

        await act.Should().ThrowAsync<DuplicateNotificationTypeException>();
    }

    [Fact]
    public async Task RetireType_rejects_when_notifications_reference_it()
    {
        var h = new ApplicationHarness();
        h.Allow(CommunityOS.Notifications.Application.Permissions.NotificationsPermissions.NotificationTypeManage);
        h.Repos.Types.GetByCodeAsync("general", Arg.Any<CancellationToken>())
            .Returns(ApplicationHarness.CreateType());
        h.Repos.Types.AnyNotificationReferencesAsync("general", Arg.Any<CancellationToken>())
            .Returns(true);

        var act = () => h.Sender.Send(new RetireNotificationTypeCommand(Actor, "general"));

        await act.Should().ThrowAsync<NotificationTypeInUseException>();
    }
}