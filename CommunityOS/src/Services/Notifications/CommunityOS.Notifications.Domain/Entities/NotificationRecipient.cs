using CommunityOS.Notifications.Domain.Enumerations;
using CommunityOS.Notifications.Domain.Exceptions;
using CommunityOS.Notifications.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;
using System.Globalization;

namespace CommunityOS.Notifications.Domain.Entities;

/// <summary>
/// Per-recipient delivery state for a notification (ADR-025, decision 4).
/// Recipient rows store **no contact details** — the stable member id is the
/// only identity; channel destinations are resolved through the Community API at
/// dispatch time and never stored. Transitions are guarded:
/// <c>Pending → Sent → Delivered → Read</c> with <c>Failed</c> terminal from
/// <c>Pending</c>/<c>Sent</c>. <c>Delivered</c>, <c>Read</c> and <c>Failed</c>
/// are terminal; no regress and no reopen.
/// </summary>
public sealed class NotificationRecipient : Entity<Guid>
{
    /// <summary>The maximum number of worker-level provider retries (bounded).</summary>
    public const int MaxRetryCount = 5;

    private NotificationRecipient() : base(Guid.Empty)
    {
        MemberId = Guid.Empty;
        Channel = NotificationChannel.InApp;
        Status = NotificationRecipientStatus.Pending;
    }

    private NotificationRecipient(
        Guid id, Guid memberId, NotificationChannel channel, NotificationRecipientStatus status)
        : base(id)
    {
        MemberId = memberId;
        Channel = channel;
        Status = status;
    }

    public Guid MemberId { get; private set; }

    public NotificationChannel Channel { get; private set; }

    public NotificationRecipientStatus Status { get; private set; }

    public DateTime? DeliveredAt { get; private set; }

    public DateTime? ReadAt { get; private set; }

    /// <summary>Stable failure-reason code (e.g. <c>provider-not-configured</c>).</summary>
    public string? FailureReason { get; private set; }

    public int RetryCount { get; private set; }

    public bool IsTerminal =>
        Status == NotificationRecipientStatus.Delivered ||
        Status == NotificationRecipientStatus.Read ||
        Status == NotificationRecipientStatus.Failed;

    public static NotificationRecipient Create(
        Guid memberId, NotificationChannel channel, NotificationRecipientStatus? status = null)
    {
        Guard.NotDefault(memberId, nameof(memberId));
        Guard.NotNull(channel, nameof(channel));
        return new NotificationRecipient(Guid.NewGuid(), memberId, channel, status ?? NotificationRecipientStatus.Pending);
    }

    /// <summary>
    /// <c>Pending → Sent</c>: handed to a channel provider. Only legal from
    /// <c>Pending</c>.
    /// </summary>
    public void MarkSent(DateTime occurredOn)
    {
        GuardTransition(NotificationRecipientStatus.Sent, [NotificationRecipientStatus.Pending]);
        Status = NotificationRecipientStatus.Sent;
        _ = occurredOn;
    }

    /// <summary>
    /// <c>→ Delivered</c>: provider ack for Email/SMS/Push, or immediate
    /// delivery for <c>InApp</c> (which skips <c>Sent</c> and delivers directly
    /// from <c>Pending</c>). Only legal from <c>Sent</c>, or from <c>Pending</c>
    /// for the <c>InApp</c> channel.
    /// </summary>
    public void MarkDelivered(DateTime occurredOn)
    {
        IReadOnlyList<NotificationRecipientStatus> allowed = Channel == NotificationChannel.InApp
            ? [NotificationRecipientStatus.Pending, NotificationRecipientStatus.Sent]
            : [NotificationRecipientStatus.Sent];

        GuardTransition(NotificationRecipientStatus.Delivered, allowed);
        Status = NotificationRecipientStatus.Delivered;
        DeliveredAt = occurredOn.ToUniversalTime();
    }

    /// <summary>
    /// <c>Delivered → Read</c>: a notification that was never delivered or that
    /// failed can never be read. Only legal from <c>Delivered</c>.
    /// </summary>
    public void MarkRead(DateTime occurredOn)
    {
        GuardTransition(NotificationRecipientStatus.Read, [NotificationRecipientStatus.Delivered]);
        Status = NotificationRecipientStatus.Read;
        ReadAt = occurredOn.ToUniversalTime();
    }

    /// <summary>
    /// <c>→ Failed</c>: terminal. Only legal from <c>Pending</c>/<c>Sent</c>.
    /// <c>FailureReason</c> records the stable cause code. No auto-re-send after
    /// terminal failure; a re-send is a new notification.
    /// </summary>
    public void MarkFailed(string reason, DateTime occurredOn)
    {
        GuardTransition(NotificationRecipientStatus.Failed, [NotificationRecipientStatus.Pending, NotificationRecipientStatus.Sent]);
        Guard.NotNullOrWhiteSpace(reason, nameof(reason));
        Guard.MaxLength(reason, 200, nameof(reason));
        Status = NotificationRecipientStatus.Failed;
        FailureReason = reason;
        _ = occurredOn;
    }

    /// <summary>
    /// Bounded worker-level provider retry counter. Provider retries are
    /// worker-level (configuration-driven backoff) and never regress persisted
    /// domain state; only the terminal outcome is persisted. The counter is
    /// capped at <see cref="MaxRetryCount"/>.
    /// </summary>
    public void IncrementRetry()
    {
        if (RetryCount >= MaxRetryCount)
            throw new InvalidNotificationTransitionException(
                MemberId, RetryCount.ToString(CultureInfo.InvariantCulture), "retry-exhausted");
        RetryCount++;
    }

    private void GuardTransition(NotificationRecipientStatus target, IReadOnlyList<NotificationRecipientStatus> allowed)
    {
        if (!allowed.Contains(Status))
            throw new InvalidNotificationTransitionException(MemberId, Status.Name, target.Name);
    }
}