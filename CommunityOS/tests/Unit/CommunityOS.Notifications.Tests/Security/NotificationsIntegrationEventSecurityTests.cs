using CommunityOS.Contracts.Notifications;
using CommunityOS.Notifications.Domain.Events;
using CommunityOS.Notifications.Domain.ValueObjects;
using CommunityOS.Notifications.Infrastructure.Integration;
using MassTransit;
using NSubstitute;

namespace CommunityOS.Notifications.Tests.Security;

/// <summary>
/// Locks the integration-event export boundary (ADR-025): the only exported
/// event is <c>NotificationDispatched</c>, carrying identifiers and a recipient
/// count only — never recipient member ids (distribution is sensitive), subject
/// or body copy, names or delivery failures. Delivered/read events are
/// domain-only and must never reach the bus.
/// </summary>
public class NotificationsIntegrationEventSecurityTests
{
    private static readonly Guid Member = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static (NotificationsIntegrationEventPublisher<TDomainEvent> Publisher, IPublishEndpoint Endpoint)
        Build<TDomainEvent>() where TDomainEvent : CommunityOS.SharedKernel.Domain.Events.IDomainEvent
    {
        var endpoint = Substitute.For<IPublishEndpoint>();
        return (new NotificationsIntegrationEventPublisher<TDomainEvent>(endpoint), endpoint);
    }

    [Fact]
    public async Task NotificationDispatched_publishes_identifiers_and_count_only()
    {
        var notification = Notification.Create(
            "task-assigned",
            NotificationChannel.InApp,
            "workflow-task",
            Guid.NewGuid(),
            MessageTemplate.Create("A task has been assigned to you", "Task {{TaskId}} assigned."),
            organizationUnitId: Guid.NewGuid(),
            additionalScopes: [],
            scheduledFor: null,
            isSensitive: false,
            [Member],
            Guid.NewGuid(),
            Now);
        notification.Queue();
        notification.Dispatch(Now);
        var dispatched = notification.DomainEvents.OfType<NotificationDispatchedEvent>().Single();

        var captured = new List<NotificationDispatched>();
        var (publisher, endpoint) = Build<NotificationDispatchedEvent>();
        endpoint.When(x => x.Publish(Arg.Any<NotificationDispatched>(), Arg.Any<CancellationToken>()))
            .Do(call => captured.Add(call.Arg<NotificationDispatched>()));

        await publisher.Handle(dispatched, CancellationToken.None);

        var published = captured.Single();
        published.NotificationId.Should().Be(notification.Id);
        published.TypeCode.Should().Be("task-assigned");
        published.Channel.Should().Be("InApp");
        published.SourceType.Should().Be("workflow-task");
        published.SourceId.Should().Be(notification.SourceId);
        published.RecipientCount.Should().Be(1);
        published.ToString().Should().NotContain(Member.ToString());
        published.ToString().Should().NotContain("assigned to you");
    }

    [Fact]
    public async Task Source_type_serializes_as_empty_string_for_free_standing_notifications()
    {
        var notification = Notification.Create(
            "general",
            NotificationChannel.InApp,
            sourceType: null,
            sourceId: null,
            MessageTemplate.Create("Notice", "A notification requires your attention."),
            organizationUnitId: null,
            additionalScopes: [],
            scheduledFor: null,
            isSensitive: false,
            [Member],
            Guid.NewGuid(),
            Now);
        notification.Queue();
        notification.Dispatch(Now);
        var dispatched = notification.DomainEvents.OfType<NotificationDispatchedEvent>().Single();

        var captured = new List<NotificationDispatched>();
        var (publisher, endpoint) = Build<NotificationDispatchedEvent>();
        endpoint.When(x => x.Publish(Arg.Any<NotificationDispatched>(), Arg.Any<CancellationToken>()))
            .Do(call => captured.Add(call.Arg<NotificationDispatched>()));

        await publisher.Handle(dispatched, CancellationToken.None);

        captured.Single().SourceType.Should().BeEmpty();
    }

    [Fact]
    public async Task Delivered_and_read_events_are_never_exported()
    {
        var (publisher, endpoint) = Build<NotificationDeliveredEvent>();

        await publisher.Handle(new NotificationDeliveredEvent(Guid.NewGuid(), Member), CancellationToken.None);

        await endpoint.DidNotReceiveWithAnyArgs().Publish(default!, default);

        var (readPublisher, readEndpoint) = Build<NotificationReadEvent>();
        await readPublisher.Handle(new NotificationReadEvent(Guid.NewGuid(), Member), CancellationToken.None);
        await readEndpoint.DidNotReceiveWithAnyArgs().Publish(default!, default);
    }
}