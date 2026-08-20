using CommunityOS.Notifications.Domain.Entities;
using CommunityOS.Notifications.Domain.ValueObjects;
using CommunityOS.Notifications.Domain.Enumerations;
using NSubstitute;

namespace CommunityOS.Notifications.Tests.Application;

/// <summary>
/// Dispatch orchestration (ADR-025): member preferences are applied before
/// dispatch — an opt-out for the (type, channel) suppresses that recipient —
/// and an InApp notification with zero surviving recipients still completes the
/// dispatch lifecycle with no external provider side effects.
/// </summary>
public class NotificationDispatchServiceTests
{
    private static readonly Guid MemberA = Guid.NewGuid();
    private static readonly Guid MemberB = Guid.NewGuid();

    [Fact]
    public async Task Opted_out_recipients_are_removed_before_dispatch()
    {
        var h = new ApplicationHarness();
        var notification = ApplicationHarness.CreateQueuedNotification(
            recipients: [MemberA, MemberB], channel: NotificationChannel.InApp);
        h.Repos.Preferences.ListDisabledAsync(
                notification.TypeCode, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([NotificationPreference.Create(MemberB, "general", NotificationChannel.InApp, enabled: false)]);

        await h.DispatchService.DispatchAsync(notification, CancellationToken.None);

        notification.Recipients.Should().ContainSingle(r => r.MemberId == MemberA);
        notification.Status.Should().Be(NotificationLifecycleStatus.Dispatched);
    }

    [Fact]
    public async Task Dispatch_is_a_noop_for_an_already_dispatched_notification()
    {
        var h = new ApplicationHarness();
        var notification = ApplicationHarness.CreateQueuedNotification(recipients: [MemberA]);
        notification.Dispatch(DateTime.UtcNow);

        await h.DispatchService.DispatchAsync(notification, CancellationToken.None);

        notification.DispatchedOn.Should().NotBeNull();
        await h.Repos.Preferences.DidNotReceiveWithAnyArgs().ListDisabledAsync(
            default!, default!, default);
    }

    [Fact]
    public async Task All_opted_out_recipients_still_completes_the_dispatch_lifecycle()
    {
        var h = new ApplicationHarness();
        var notification = ApplicationHarness.CreateQueuedNotification(
            recipients: [MemberA], channel: NotificationChannel.InApp);
        h.Repos.Preferences.ListDisabledAsync(
                notification.TypeCode, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([NotificationPreference.Create(MemberA, "general", NotificationChannel.InApp, enabled: false)]);

        await h.DispatchService.DispatchAsync(notification, CancellationToken.None);

        notification.Recipients.Should().BeEmpty();
        notification.Status.Should().Be(NotificationLifecycleStatus.Dispatched);
    }
}