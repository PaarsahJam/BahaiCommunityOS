using CommunityOS.Notifications.Domain.Constants;
using CommunityOS.Notifications.Domain.Entities;
using CommunityOS.Notifications.Domain.Enumerations;
using CommunityOS.Notifications.Domain.Exceptions;
using CommunityOS.Notifications.Domain.Events;
using CommunityOS.Notifications.Domain.ValueObjects;

namespace CommunityOS.Notifications.Tests.Domain;

/// <summary>
/// Domain-invariant tests for the Notification aggregate (ADR-025): the guarded
/// lifecycle <c>Draft → Queued → Dispatched</c>, first-gate channel semantics
/// (<c>InApp</c> delivers immediately; Email/SMS/Push fail closed as
/// <c>provider-not-configured</c>), double-dispatch protection, recipient
/// mutability while Draft/Queued only, both-or-neither source reference, and the
/// read rule (delivered only).
/// </summary>
public class NotificationLifecycleTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid MemberA = Guid.NewGuid();
    private static readonly Guid MemberB = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static Notification Create(
        NotificationChannel? channel = null,
        IReadOnlyList<Guid>? recipients = null,
        Guid? sourceId = null,
        string? sourceType = null,
        bool isSensitive = false) =>
        Notification.Create(
            "general",
            channel ?? NotificationChannel.InApp,
            sourceType,
            sourceId,
            MessageTemplate.Create("Subject", "Body {{Var}}"),
            organizationUnitId: Guid.NewGuid(),
            additionalScopes: [],
            scheduledFor: null,
            isSensitive,
            recipients ?? [MemberA, MemberB],
            Actor,
            Now);

    // --- Creation ---

    [Fact]
    public void Create_starts_as_Draft_with_pending_recipients()
    {
        var notification = Create();

        notification.Status.Should().Be(NotificationLifecycleStatus.Draft);
        notification.Recipients.Should().HaveCount(2);
        notification.Recipients.Should().OnlyContain(r =>
            r.Status == NotificationRecipientStatus.Pending);
    }

    [Fact]
    public void Create_requires_both_or_neither_source_reference()
    {
        var act = () => Create(sourceId: Guid.NewGuid(), sourceType: null);

        act.Should().Throw<InvalidNotificationException>();
    }

    [Fact]
    public void Create_deduplicates_recipient_ids()
    {
        var notification = Create(recipients: [MemberA, MemberA]);

        notification.Recipients.Should().ContainSingle();
    }

    // --- Queue ---

    [Fact]
    public void Queue_requires_at_least_one_recipient()
    {
        var notification = Create(recipients: []);

        var act = () => notification.Queue();

        act.Should().Throw<InvalidNotificationException>();
    }

    [Fact]
    public void Queue_transitions_Draft_to_Queued_once()
    {
        var notification = Create();
        notification.Queue();

        notification.Status.Should().Be(NotificationLifecycleStatus.Queued);

        var act = () => notification.Queue();
        act.Should().Throw<InvalidNotificationTransitionException>();
    }

    // --- Dispatch (first gate) ---

    [Fact]
    public void Dispatch_cannot_run_before_Queue()
    {
        var notification = Create();

        var act = () => notification.Dispatch(Now);

        act.Should().Throw<InvalidNotificationTransitionException>();
    }

    [Fact]
    public void InApp_recipients_deliver_immediately_on_dispatch()
    {
        var notification = Create(recipients: [MemberA]);
        notification.Queue();
        notification.Dispatch(Now);

        notification.Status.Should().Be(NotificationLifecycleStatus.Dispatched);
        notification.DispatchedOn.Should().NotBeNull();
        notification.Recipients.Single().Status.Should().Be(NotificationRecipientStatus.Delivered);
        notification.DomainEvents.Should().ContainSingle(e => e is NotificationDispatchedEvent);
    }

    [Fact]
    public void Email_recipients_fail_closed_with_provider_not_configured()
    {
        var notification = Create(channel: NotificationChannel.Email, recipients: [MemberA]);
        notification.Queue();
        notification.Dispatch(Now);

        notification.Status.Should().Be(NotificationLifecycleStatus.Dispatched);
        var recipient = notification.Recipients.Single();
        recipient.Status.Should().Be(NotificationRecipientStatus.Failed);
        recipient.FailureReason.Should().Be(NotificationFailureReasons.ProviderNotConfigured);
    }

    [Fact]
    public void Dispatched_notifications_raise_event_with_stable_fields_only()
    {
        var notification = Create(
            channel: NotificationChannel.InApp,
            recipients: [MemberA],
            sourceType: "workflow-task",
            sourceId: Guid.NewGuid());
        notification.Queue();
        notification.Dispatch(Now);

        var @event = notification.DomainEvents.OfType<NotificationDispatchedEvent>().Single();
        @event.NotificationId.Should().Be(notification.Id);
        @event.TypeCode.Should().Be("general");
        @event.Channel.Should().Be("InApp");
        @event.SourceType.Should().Be("workflow-task");
        @event.SourceId.Should().NotBeNull();
        @event.RecipientCount.Should().Be(1);
    }

    [Fact]
    public void Double_dispatch_throws()
    {
        var notification = Create(recipients: [MemberA]);
        notification.Queue();
        notification.Dispatch(Now);

        var act = () => notification.Dispatch(Now);

        act.Should().Throw<InvalidNotificationTransitionException>();
    }

    // --- Recipient mutability ---

    [Fact]
    public void Recipients_can_be_added_and_removed_while_queued()
    {
        var notification = Create(recipients: [MemberA]);
        notification.Queue();

        notification.AddRecipient(MemberB);
        notification.Recipients.Should().HaveCount(2);

        notification.RemoveRecipient(MemberB);
        notification.Recipients.Should().ContainSingle();
    }

    [Fact]
    public void Dispatched_notifications_reject_recipient_changes()
    {
        var notification = Create(recipients: [MemberA]);
        notification.Queue();
        notification.Dispatch(Now);

        var act = () => notification.AddRecipient(MemberB);

        act.Should().Throw<InvalidNotificationTransitionException>();
    }

    // --- Read ---

    [Fact]
    public void Delivered_recipient_can_mark_read()
    {
        var notification = Create(recipients: [MemberA]);
        notification.Queue();
        notification.Dispatch(Now);

        notification.MarkRead(MemberA, Now);

        notification.Recipients.Single().Status.Should().Be(NotificationRecipientStatus.Read);
        notification.DomainEvents.Should().Contain(e => e is NotificationReadEvent);
    }

    [Fact]
    public void Non_recipient_cannot_mark_read()
    {
        var notification = Create(recipients: [MemberA]);
        notification.Queue();
        notification.Dispatch(Now);

        var act = () => notification.MarkRead(MemberB, Now);

        act.Should().Throw<InvalidMemberReferenceException>();
    }

    [Fact]
    public void Failed_recipient_can_never_be_read()
    {
        var notification = Create(channel: NotificationChannel.Email, recipients: [MemberA]);
        notification.Queue();
        notification.Dispatch(Now);

        var act = () => notification.MarkRead(MemberA, Now);

        act.Should().Throw<InvalidNotificationTransitionException>();
    }
}

/// <summary>
/// Per-recipient transition guards (ADR-025, decision 4): the bounded retry
/// counter, the <c>Pending → Sent → Delivered → Read</c> chain, and the terminal
/// <c>Failed</c> outcome with a stable failure reason.
/// </summary>
public class NotificationRecipientTests
{
    private static readonly Guid Member = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public void Sent_then_Delivered_then_Read_walks_the_chain()
    {
        var recipient = NotificationRecipient.Create(Member, NotificationChannel.Email);

        recipient.MarkSent(Now);
        recipient.Status.Should().Be(NotificationRecipientStatus.Sent);

        recipient.MarkDelivered(Now);
        recipient.Status.Should().Be(NotificationRecipientStatus.Delivered);
        recipient.DeliveredAt.Should().NotBeNull();

        recipient.MarkRead(Now);
        recipient.Status.Should().Be(NotificationRecipientStatus.Read);
        recipient.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public void InApp_can_deliver_directly_from_pending()
    {
        var recipient = NotificationRecipient.Create(Member, NotificationChannel.InApp);

        recipient.MarkDelivered(Now);

        recipient.Status.Should().Be(NotificationRecipientStatus.Delivered);
    }

    [Fact]
    public void Failed_is_terminal_and_records_the_stable_reason()
    {
        var recipient = NotificationRecipient.Create(Member, NotificationChannel.Sms);

        recipient.MarkFailed(NotificationFailureReasons.ProviderNotConfigured, Now);

        recipient.Status.Should().Be(NotificationRecipientStatus.Failed);
        recipient.FailureReason.Should().Be(NotificationFailureReasons.ProviderNotConfigured);
        recipient.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void Retry_counter_is_bounded_by_max_retry_count()
    {
        var recipient = NotificationRecipient.Create(Member, NotificationChannel.Email);

        for (var i = 0; i < NotificationRecipient.MaxRetryCount; i++)
            recipient.IncrementRetry();

        recipient.RetryCount.Should().Be(NotificationRecipient.MaxRetryCount);

        var act = () => recipient.IncrementRetry();
        act.Should().Throw<InvalidNotificationTransitionException>();
    }

    [Fact]
    public void Failed_recipient_cannot_be_delivered()
    {
        var recipient = NotificationRecipient.Create(Member, NotificationChannel.Email);
        recipient.MarkFailed(NotificationFailureReasons.ProviderNotConfigured, Now);

        var act = () => recipient.MarkDelivered(Now);

        act.Should().Throw<InvalidNotificationTransitionException>();
    }
}