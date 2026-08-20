using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Notifications.Application.Permissions;
using CommunityOS.Notifications.Application.Queries;
using CommunityOS.Notifications.Domain.Exceptions;
using NSubstitute;

namespace CommunityOS.Notifications.Tests.Application;

/// <summary>
/// Query-level behavior (ADR-025): the inbox exposes only notifications the
/// caller may read (recipient or scope read; sensitive notifications also need
/// the sensitive capability), and single-item reads surface 404 for anything
/// the caller cannot read — no existence oracle.
/// </summary>
public class NotificationQueryTests
{
    private static readonly Guid Actor = ApplicationTestData.Actor;

    [Fact]
    public async Task Inbox_returns_recipient_notifications_readable_at_any_scope()
    {
        var h = new ApplicationHarness();
        h.AllowBatch(
            NotificationsPermissions.NotificationRead,
            NotificationsPermissions.NotificationReadSensitive);
        var notification = ApplicationHarness.CreateQueuedNotification(
            recipients: [Actor], organizationUnitId: Guid.NewGuid());
        h.Repos.Notifications.ListInboxCandidatesAsync(Actor, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([notification]);

        var result = await h.Sender.Send(new ListInboxQuery(Actor, null, null, null, null, 50, 0));

        result.Should().ContainSingle(d => d.Id == notification.Id);
        await h.Repos.Evaluator.Received(1).EvaluateBatchAsync(
            Arg.Any<IReadOnlyList<AuthorizationRequest>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Inbox_excludes_notifications_without_any_scope_read_grant()
    {
        var h = new ApplicationHarness();
        h.DenyAll();
        var notification = ApplicationHarness.CreateQueuedNotification(
            recipients: [Actor], organizationUnitId: Guid.NewGuid());
        h.Repos.Notifications.ListInboxCandidatesAsync(Actor, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([notification]);

        var result = await h.Sender.Send(new ListInboxQuery(Actor, null, null, null, null, 50, 0));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Inbox_excludes_sensitive_notifications_without_the_sensitive_capability()
    {
        var h = new ApplicationHarness();
        // Read allowed at scope, but the sensitive capability is denied.
        h.AllowBatch(NotificationsPermissions.NotificationRead);
        var notification = ApplicationHarness.CreateQueuedNotification(
            recipients: [Actor], organizationUnitId: Guid.NewGuid(), isSensitive: true);
        h.Repos.Notifications.ListInboxCandidatesAsync(Actor, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([notification]);

        var result = await h.Sender.Send(new ListInboxQuery(Actor, null, null, null, null, 50, 0));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Inbox_applies_type_filter_after_authorization()
    {
        var h = new ApplicationHarness();
        h.AllowBatch(
            NotificationsPermissions.NotificationRead,
            NotificationsPermissions.NotificationReadSensitive);
        var notification = ApplicationHarness.CreateQueuedNotification(
            recipients: [Actor], organizationUnitId: Guid.NewGuid());
        h.Repos.Notifications.ListInboxCandidatesAsync(Actor, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([notification]);

        var result = await h.Sender.Send(new ListInboxQuery(Actor, "task-assigned", null, null, null, 50, 0));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetNotification_returns_404_for_a_non_recipient_without_scope_read()
    {
        var h = new ApplicationHarness();
        h.DenyAll();
        var other = Guid.NewGuid();
        var notification = ApplicationHarness.CreateQueuedNotification(
            recipients: [other], organizationUnitId: Guid.NewGuid());
        h.Repos.Notifications.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>())
            .Returns(notification);

        var act = () => h.Sender.Send(new GetNotificationQuery(Actor, notification.Id));

        await act.Should().ThrowAsync<NotificationNotFoundException>();
    }

    [Fact]
    public async Task GetNotification_returns_404_for_a_missing_notification()
    {
        var h = new ApplicationHarness();
        h.Repos.Notifications.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Notification?)null);

        var act = () => h.Sender.Send(new GetNotificationQuery(Actor, Guid.NewGuid()));

        await act.Should().ThrowAsync<NotificationNotFoundException>();
    }

    [Fact]
    public async Task GetNotificationSensitive_requires_the_sensitive_capability()
    {
        var h = new ApplicationHarness();
        // Recipient readable, but no sensitive capability granted.
        h.AllowBatch(NotificationsPermissions.NotificationRead);
        var notification = ApplicationHarness.CreateQueuedNotification(recipients: [Actor]);
        h.Repos.Notifications.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>())
            .Returns(notification);

        var act = () => h.Sender.Send(new GetNotificationSensitiveQuery(Actor, notification.Id));

        await act.Should().ThrowAsync<NotificationNotFoundException>();
    }

    [Fact]
    public async Task GetNotificationSensitive_returns_distribution_for_authorized_reader()
    {
        var h = new ApplicationHarness();
        h.AllowBatch(
            NotificationsPermissions.NotificationRead,
            NotificationsPermissions.NotificationReadSensitive);
        var notification = ApplicationHarness.CreateQueuedNotification(recipients: [Actor]);
        h.Repos.Notifications.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>())
            .Returns(notification);

        var dto = await h.Sender.Send(new GetNotificationSensitiveQuery(Actor, notification.Id));

        dto.NotificationId.Should().Be(notification.Id);
        dto.Distribution.Should().ContainSingle(r => r.MemberId == Actor);
    }
}