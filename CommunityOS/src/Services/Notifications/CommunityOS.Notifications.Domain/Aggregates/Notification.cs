using CommunityOS.Notifications.Domain.Constants;
using CommunityOS.Notifications.Domain.Entities;
using CommunityOS.Notifications.Domain.Enumerations;
using CommunityOS.Notifications.Domain.Events;
using CommunityOS.Notifications.Domain.Exceptions;
using CommunityOS.Notifications.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Notifications.Domain.Aggregates;

/// <summary>
/// Aggregate root of the Notifications bounded context (ADR-025): a notification
/// instance referencing a triggering domain fact (stable <c>SourceType</c> +
/// <c>SourceId</c>) or free-standing <c>general</c> work, with a
/// notification-type code, a channel, a subject/body template, a primary
/// organization scope plus additional scopes, an optional <c>ScheduledFor</c>, a
/// lifecycle status, an <c>IsSensitive</c> flag and a recipient set.
///
/// Lifecycle <c>Draft → Queued → Dispatched</c>. <c>Dispatched</c> is terminal:
/// reached only when every recipient is terminal, in the same transaction that
/// raises <c>NotificationDispatchedEvent</c>. Dispatch on an already-dispatched
/// notification throws (double-dispatch protection). Recipients may be added or
/// removed while <c>Draft</c>/<c>Queued</c> only; a re-send is always a **new**
/// notification (Workflow "no reopen" philosophy).
///
/// Delivery is orchestration, never domain authority: a notification never
/// changes the lifecycle of the record, task, question or document it refers to.
/// Recipients are stable member ids; no PII, names or destinations are stored.
/// </summary>
public sealed class Notification : AggregateRoot<Guid>
{
    private readonly List<NotificationRecipient> _recipients = [];
    private readonly List<NotificationScope> _scopes = [];

    private Notification() : base(Guid.Empty)
    {
        TypeCode = null!;
        Channel = NotificationChannel.InApp;
        Template = null!;
        Status = NotificationLifecycleStatus.Draft;
        IsSensitive = false;
    }

    private Notification(
        Guid id,
        string typeCode,
        NotificationChannel channel,
        string? sourceType,
        Guid? sourceId,
        MessageTemplate template,
        Guid? organizationUnitId,
        IReadOnlyList<Guid> additionalScopes,
        DateTime? scheduledFor,
        bool isSensitive,
        IReadOnlyList<Guid> recipientIds,
        Guid createdBy,
        DateTime occurredOn) : base(id)
    {
        TypeCode = typeCode;
        Channel = channel;
        SourceType = sourceType;
        SourceId = sourceId;
        Template = template;
        OrganizationUnitId = organizationUnitId;
        ScheduledFor = scheduledFor;
        IsSensitive = isSensitive;
        CreatedBy = createdBy;
        CreatedOn = occurredOn.ToUniversalTime();
        Status = NotificationLifecycleStatus.Draft;

        _scopes.AddRange(
            additionalScopes.Distinct().Select(s => new NotificationScope(Guid.NewGuid(), s)));

        foreach (var memberId in recipientIds.Distinct())
            _recipients.Add(NotificationRecipient.Create(memberId, channel));
    }

    public string TypeCode { get; private set; }

    public NotificationChannel Channel { get; private set; }

    /// <summary>Stable source-fact type (e.g. <c>workflow-task</c>); null for free-standing <c>general</c> work.</summary>
    public string? SourceType { get; private set; }

    /// <summary>Stable source-fact id; null for free-standing <c>general</c> work.</summary>
    public Guid? SourceId { get; private set; }

    public MessageTemplate Template { get; private set; }

    /// <summary>Primary organization scope. Nullable for notifications without a unit scope.</summary>
    public Guid? OrganizationUnitId { get; private set; }

    public NotificationLifecycleStatus Status { get; private set; }

    public bool IsSensitive { get; private set; }

    public DateTime? ScheduledFor { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public DateTime? DispatchedOn { get; private set; }

    public IReadOnlyList<NotificationRecipient> Recipients => _recipients.AsReadOnly();

    public IReadOnlyList<NotificationScope> AdditionalScopes => _scopes.AsReadOnly();

    /// <summary>Every organization-unit scope: primary plus additional scopes.</summary>
    public IEnumerable<Guid> AllOrganizationUnitIds =>
        new[] { OrganizationUnitId }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Concat(_scopes.Select(s => s.OrganizationUnitId))
            .Distinct();

    public bool IsDispatched => Status == NotificationLifecycleStatus.Dispatched;

    public bool IsRecipient(Guid memberId) =>
        _recipients.Any(r => r.MemberId == memberId);

    public NotificationRecipient? RecipientFor(Guid memberId) =>
        _recipients.FirstOrDefault(r => r.MemberId == memberId);

    /// <summary>
    /// Creates a notification in <c>Draft</c>. Creation is idempotent per
    /// (type, source type, source id, channel) at the repository boundary; the
    /// aggregate always creates a new instance. Both-or-neither rule for the
    /// source reference: a notification is either bound to a stable domain fact
    /// (<c>SourceType</c> + <c>SourceId</c>) or free-standing.
    /// </summary>
    public static Notification Create(
        string typeCode,
        NotificationChannel channel,
        string? sourceType,
        Guid? sourceId,
        MessageTemplate template,
        Guid? organizationUnitId,
        IReadOnlyList<Guid> additionalScopes,
        DateTime? scheduledFor,
        bool isSensitive,
        IReadOnlyList<Guid> recipientIds,
        Guid createdBy,
        DateTime occurredOn)
    {
        Guard.NotNullOrWhiteSpace(typeCode, nameof(typeCode));
        Guard.MaxLength(typeCode, 100, nameof(typeCode));
        Guard.NotNull(channel, nameof(channel));
        Guard.NotNull(template, nameof(template));
        Guard.NotDefault(createdBy, nameof(createdBy));

        if (sourceType is null != sourceId is null)
            throw new InvalidNotificationException(
                "A notification source reference requires both SourceType and SourceId (or neither).");

        if (sourceType is not null)
            Guard.MaxLength(sourceType, 50, nameof(sourceType));

        foreach (var memberId in recipientIds ?? [])
            Guard.NotDefault(memberId, nameof(memberId));

        var notification = new Notification(
            Guid.NewGuid(),
            typeCode.Trim(),
            channel,
            sourceType,
            sourceId,
            template,
            organizationUnitId,
            additionalScopes ?? [],
            scheduledFor,
            isSensitive,
            recipientIds ?? [],
            createdBy,
            occurredOn);

        return notification;
    }

    /// <summary>
    /// Adds a recipient (dedup: no-op when the member is already a recipient).
    /// Only legal while <c>Draft</c>/<c>Queued</c>; dispatched notifications are
    /// immutable.
    /// </summary>
    public void AddRecipient(Guid memberId)
    {
        Guard.NotDefault(memberId, nameof(memberId));
        GuardNotTerminal("add a recipient");
        if (_recipients.Any(r => r.MemberId == memberId)) return;
        _recipients.Add(NotificationRecipient.Create(memberId, Channel));
    }

    /// <summary>
    /// Removes a recipient (no-op when absent). Only legal while
    /// <c>Draft</c>/<c>Queued</c>; dispatched notifications are immutable.
    /// </summary>
    public void RemoveRecipient(Guid memberId)
    {
        GuardNotTerminal("remove a recipient");
        var recipient = _recipients.FirstOrDefault(r => r.MemberId == memberId);
        if (recipient is not null)
            _recipients.Remove(recipient);
    }

    /// <summary>
    /// <c>Draft → Queued</c>: the notification is ready for dispatch (immediate
    /// or at <c>ScheduledFor</c>). Requires at least one recipient.
    /// </summary>
    public void Queue()
    {
        if (Status != NotificationLifecycleStatus.Draft)
            throw new InvalidNotificationTransitionException(Id, Status.Name, NotificationLifecycleStatus.Queued.Name);

        if (_recipients.Count == 0)
            throw new InvalidNotificationException("A notification cannot be queued without at least one recipient.");

        Status = NotificationLifecycleStatus.Queued;
    }

    /// <summary>
    /// Triggers dispatch. Only legal from <c>Queued</c>; invoking on an
    /// already-<c>Dispatched</c> notification throws (double-dispatch
    /// protection). First-gate channel semantics (ADR-025, decision 5):
    /// <c>InApp</c> delivers immediately (<c>Pending → Delivered</c>, no external
    /// provider); Email/SMS/Push fail closed (<c>Failed</c>, reason
    /// <c>provider-not-configured</c>). <c>Dispatched</c> is reached only when
    /// every recipient is terminal, in the same transaction that raises
    /// <c>NotificationDispatchedEvent</c>.
    /// </summary>
    public void Dispatch(DateTime occurredOn)
    {
        if (Status == NotificationLifecycleStatus.Dispatched)
            throw new InvalidNotificationTransitionException(Id, Status.Name, NotificationLifecycleStatus.Dispatched.Name);

        if (Status != NotificationLifecycleStatus.Queued)
            throw new InvalidNotificationTransitionException(Id, Status.Name, NotificationLifecycleStatus.Dispatched.Name);

        foreach (var recipient in _recipients.Where(r => !r.IsTerminal))
        {
            if (recipient.Channel == NotificationChannel.InApp)
                recipient.MarkDelivered(occurredOn);
            else
                recipient.MarkFailed(NotificationFailureReasons.ProviderNotConfigured, occurredOn);
        }

        if (_recipients.All(r => r.IsTerminal))
        {
            Status = NotificationLifecycleStatus.Dispatched;
            DispatchedOn = occurredOn.ToUniversalTime();
            RaiseDomainEvent(new NotificationDispatchedEvent(
                Id, TypeCode, Channel.Name, SourceType, SourceId, _recipients.Count));
        }
    }

    /// <summary>
    /// <c>Delivered → Read</c> for one recipient. The actor must be the recipient
    /// (relationship tuple); a notification that was never delivered or that
    /// failed can never be read. Raises <c>NotificationReadEvent</c> (domain-only,
    /// never exported).
    /// </summary>
    public void MarkRead(Guid memberId, DateTime occurredOn)
    {
        var recipient = RecipientFor(memberId)
            ?? throw new InvalidMemberReferenceException(memberId);

        recipient.MarkRead(occurredOn);
        RaiseDomainEvent(new NotificationReadEvent(Id, memberId));
    }

    private void GuardNotTerminal(string operation)
    {
        if (Status == NotificationLifecycleStatus.Dispatched)
            throw new InvalidNotificationTransitionException(
                Id, Status.Name, $"cannot {operation} on a dispatched notification");
    }
}