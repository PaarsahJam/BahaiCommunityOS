using CommunityOS.Notifications.Application.DTOs;
using CommunityOS.Notifications.Application.Queries;
using NSubstitute;

namespace CommunityOS.Notifications.Tests.Application;

/// <summary>
/// Member read contract (ADR-027): recipient-scoped listing with content and
/// recipient-specific read state, recipient-scoped unread count, and bounded
/// server-side pagination. No client-supplied recipient identity exists on the
/// surface; the actor id always comes from the query, never from the client.
/// </summary>
public class MemberNotificationQueryTests
{
    private static readonly Guid Actor = ApplicationTestData.Actor;

    [Fact]
    public async Task List_returns_recipient_notifications_with_content_and_unread_state()
    {
        var h = new ApplicationHarness();
        var notification = ApplicationHarness.CreateQueuedNotification(recipients: [Actor]);
        notification.Dispatch(DateTime.UtcNow);
        h.Repos.Notifications.ListMemberInboxAsync(
                Actor, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([notification]);

        var result = await h.Sender.Send(new ListMemberNotificationsQuery(Actor, 50, 0));

        var dto = result.Should().ContainSingle().Subject;
        dto.Id.Should().Be(notification.Id);
        dto.TypeCode.Should().Be("general");
        dto.Channel.Should().Be("InApp");
        dto.Title.Should().Be("Subject");
        dto.Body.Should().Be("Body");
        dto.IsRead.Should().BeFalse();
        dto.ReadAt.Should().BeNull();
        dto.CreatedOn.Should().Be(notification.CreatedOn);
    }

    [Fact]
    public async Task List_reflects_recipient_specific_read_state()
    {
        var h = new ApplicationHarness();
        var notification = ApplicationHarness.CreateQueuedNotification(recipients: [Actor]);
        notification.Dispatch(DateTime.UtcNow);
        notification.MarkRead(Actor, DateTime.UtcNow);
        h.Repos.Notifications.ListMemberInboxAsync(
                Actor, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([notification]);

        var result = await h.Sender.Send(new ListMemberNotificationsQuery(Actor, 50, 0));

        var dto = result.Should().ContainSingle().Subject;
        dto.IsRead.Should().BeTrue();
        dto.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task List_passes_actor_without_any_client_supplied_recipient_identity()
    {
        var h = new ApplicationHarness();
        h.Repos.Notifications.ListMemberInboxAsync(
                Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await h.Sender.Send(new ListMemberNotificationsQuery(Actor, 50, 0));

        await h.Repos.Notifications.Received(1).ListMemberInboxAsync(
            Actor, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_over_fetches_limit_plus_one_and_caps_the_result()
    {
        const int Limit = 3;
        var h = new ApplicationHarness();
        var notifications = Enumerable.Range(0, Limit + 1)
            .Select(_ => ApplicationHarness.CreateQueuedNotification(recipients: [Actor]))
            .ToList();
        h.Repos.Notifications.ListMemberInboxAsync(
                Actor, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(notifications);

        var result = await h.Sender.Send(new ListMemberNotificationsQuery(Actor, Limit, 0));

        result.Should().HaveCount(Limit);
        await h.Repos.Notifications.Received(1).ListMemberInboxAsync(
            Actor, limit: Limit + 1, offset: 0, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(500)]
    public async Task List_clamps_page_size_to_the_maximum(int requested)
    {
        var h = new ApplicationHarness();
        h.Repos.Notifications.ListMemberInboxAsync(
                Actor, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await h.Sender.Send(new ListMemberNotificationsQuery(Actor, requested, 0));

        // Max page size is 100; the repository over-fetch is limit + 1.
        await h.Repos.Notifications.Received(1).ListMemberInboxAsync(
            Actor, limit: ListMemberNotificationsQuery.MaxPageSize + 1,
            offset: 0, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnreadCount_is_recipient_scoped_for_the_actor()
    {
        var h = new ApplicationHarness();
        h.Repos.Notifications.CountUnreadByMemberAsync(Actor, Arg.Any<CancellationToken>())
            .Returns(3);

        var dto = await h.Sender.Send(new GetMemberUnreadCountQuery(Actor));

        dto.Should().Be(new MemberUnreadCountDto(3));
        await h.Repos.Notifications.Received(1).CountUnreadByMemberAsync(
            Actor, Arg.Any<CancellationToken>());
    }
}