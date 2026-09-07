using CommunityOS.Notifications.Application.Commands;
using CommunityOS.Notifications.Domain.Enumerations;
using CommunityOS.Notifications.Domain.Exceptions;
using NSubstitute;

namespace CommunityOS.Notifications.Tests.Application;

/// <summary>
/// Member mark-as-read (ADR-027): only the authenticated recipient may change
/// their own read state; no client-supplied <c>memberId</c> exists on the
/// surface; sensitive notifications and non-recipients surface the equivalent
/// 404 (no oracle, no recipient enumeration).
/// </summary>
public class MemberNotificationCommandTests
{
    private static readonly Guid Actor = ApplicationTestData.Actor;
    private static readonly Guid Recipient = ApplicationTestData.Recipient;

    [Fact]
    public async Task MarkRead_by_the_recipient_transitions_to_read_and_returns_member_summary()
    {
        var h = new ApplicationHarness();
        var notification = ApplicationHarness.CreateQueuedNotification(recipients: [Actor]);
        notification.Dispatch(DateTime.UtcNow);
        h.Repos.Notifications.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>())
            .Returns(notification);

        var dto = await h.Sender.Send(new MarkMemberNotificationReadCommand(Actor, notification.Id));

        dto.Id.Should().Be(notification.Id);
        dto.IsRead.Should().BeTrue();
        dto.ReadAt.Should().NotBeNull();
        await h.Repos.Notifications.Received(1).UpdateAsync(notification, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkRead_by_a_non_recipient_is_404()
    {
        var h = new ApplicationHarness();
        var other = Guid.NewGuid();
        var notification = ApplicationHarness.CreateQueuedNotification(recipients: [Recipient]);
        notification.Dispatch(DateTime.UtcNow);
        h.Repos.Notifications.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>())
            .Returns(notification);

        var act = () => h.Sender.Send(new MarkMemberNotificationReadCommand(other, notification.Id));

        await act.Should().ThrowAsync<NotificationNotFoundException>();
        await h.Repos.Notifications.DidNotReceive()
            .UpdateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkRead_by_the_recipient_of_a_sensitive_notification_is_404()
    {
        var h = new ApplicationHarness();
        var notification = ApplicationHarness.CreateQueuedNotification(
            recipients: [Actor], isSensitive: true);
        notification.Dispatch(DateTime.UtcNow);
        h.Repos.Notifications.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>())
            .Returns(notification);

        var act = () => h.Sender.Send(new MarkMemberNotificationReadCommand(Actor, notification.Id));

        await act.Should().ThrowAsync<NotificationNotFoundException>();
        await h.Repos.Notifications.DidNotReceive()
            .UpdateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkRead_cannot_alter_another_recipients_read_state()
    {
        var h = new ApplicationHarness();
        // A shared notification for Actor and Recipient: the actor may only
        // transition their own recipient row; the other recipient's state is
        // untouched.
        var notification = ApplicationHarness.CreateQueuedNotification(
            recipients: [Actor, Recipient]);
        notification.Dispatch(DateTime.UtcNow);
        h.Repos.Notifications.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>())
            .Returns(notification);

        await h.Sender.Send(new MarkMemberNotificationReadCommand(Actor, notification.Id));

        notification.RecipientFor(Actor)!.Status.Should().Be(NotificationRecipientStatus.Read);
        notification.RecipientFor(Recipient)!.Status.Should().Be(NotificationRecipientStatus.Delivered);
        notification.RecipientFor(Recipient)!.ReadAt.Should().BeNull();
    }
}