using CommunityOS.Notifications.Domain.Exceptions;
using CommunityOS.Notifications.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Notifications.Domain.Aggregates;

/// <summary>
/// A notification-type catalog entry (ADR-025, decision 6): stable string code,
/// display name, default channel, and default subject/body templates with
/// <c>{{var}}</c> placeholders, plus an <c>IsSensitive</c> flag. Types are
/// configuration: seeded idempotently at migration time
/// (<c>NotificationCatalogSeeder</c>), extensible at runtime, retired (never
/// deleted). The baseline is <c>task-assigned, task-escalated, task-completed,
/// task-cancelled, record-verified, record-rejected, record-hold (sensitive),
/// question-flagged, community-activity, community-event, community-meeting,
/// general</c>.
/// </summary>
public sealed class NotificationType : AggregateRoot<Guid>
{
    private NotificationType() : base(Guid.Empty)
    {
        Code = null!;
        DisplayName = null!;
        DefaultChannel = NotificationChannel.InApp;
        SubjectTemplate = null!;
        BodyTemplate = null!;
    }

    private NotificationType(
        Guid id,
        string code,
        string displayName,
        NotificationChannel defaultChannel,
        string subjectTemplate,
        string bodyTemplate,
        bool isSensitive,
        Guid createdBy,
        DateTime occurredOn) : base(id)
    {
        Code = code;
        DisplayName = displayName;
        DefaultChannel = defaultChannel;
        SubjectTemplate = subjectTemplate;
        BodyTemplate = bodyTemplate;
        IsSensitive = isSensitive;
        IsActive = true;
        CreatedBy = createdBy;
        CreatedOn = occurredOn.ToUniversalTime();
        UpdatedBy = createdBy;
        UpdatedOn = occurredOn.ToUniversalTime();
    }

    public string Code { get; private set; }

    public string DisplayName { get; private set; }

    public NotificationChannel DefaultChannel { get; private set; }

    public string SubjectTemplate { get; private set; }

    public string BodyTemplate { get; private set; }

    public bool IsSensitive { get; private set; }

    public bool IsActive { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public Guid UpdatedBy { get; private set; }

    public DateTime UpdatedOn { get; private set; }

    public Guid? RetiredBy { get; private set; }

    public DateTime? RetiredOn { get; private set; }

    public static NotificationType Create(
        string code,
        string displayName,
        NotificationChannel defaultChannel,
        string subjectTemplate,
        string bodyTemplate,
        bool isSensitive,
        Guid createdBy,
        DateTime occurredOn)
    {
        Guard.NotNullOrWhiteSpace(code, nameof(code));
        Guard.MaxLength(code, 100, nameof(code));
        Guard.NotNullOrWhiteSpace(displayName, nameof(displayName));
        Guard.MaxLength(displayName, 200, nameof(displayName));
        Guard.NotNull(defaultChannel, nameof(defaultChannel));
        Guard.NotNullOrWhiteSpace(subjectTemplate, nameof(subjectTemplate));
        Guard.MaxLength(subjectTemplate, 500, nameof(subjectTemplate));
        Guard.NotNullOrWhiteSpace(bodyTemplate, nameof(bodyTemplate));
        Guard.MaxLength(bodyTemplate, 2000, nameof(bodyTemplate));
        Guard.NotDefault(createdBy, nameof(createdBy));

        return new NotificationType(
            Guid.NewGuid(),
            code.Trim().ToLowerInvariant(),
            displayName.Trim(),
            defaultChannel,
            subjectTemplate.Trim(),
            bodyTemplate.Trim(),
            isSensitive,
            createdBy,
            occurredOn);
    }

    public void Update(
        string displayName,
        NotificationChannel defaultChannel,
        string subjectTemplate,
        string bodyTemplate,
        bool isSensitive,
        Guid updatedBy,
        DateTime occurredOn)
    {
        if (!IsActive)
            throw new RetiredNotificationTypeUpdateException(Code);

        Guard.NotNullOrWhiteSpace(displayName, nameof(displayName));
        Guard.MaxLength(displayName, 200, nameof(displayName));
        Guard.NotNull(defaultChannel, nameof(defaultChannel));
        Guard.NotNullOrWhiteSpace(subjectTemplate, nameof(subjectTemplate));
        Guard.MaxLength(subjectTemplate, 500, nameof(subjectTemplate));
        Guard.NotNullOrWhiteSpace(bodyTemplate, nameof(bodyTemplate));
        Guard.MaxLength(bodyTemplate, 2000, nameof(bodyTemplate));
        Guard.NotDefault(updatedBy, nameof(updatedBy));

        DisplayName = displayName.Trim();
        DefaultChannel = defaultChannel;
        SubjectTemplate = subjectTemplate.Trim();
        BodyTemplate = bodyTemplate.Trim();
        IsSensitive = isSensitive;
        UpdatedBy = updatedBy;
        UpdatedOn = occurredOn.ToUniversalTime();
    }

    /// <summary>
    /// Retires the type (never deletes). Only legal when the type is not in use
    /// (no notifications reference it); the repository/handler enforces the
    /// in-use guard before calling this.
    /// </summary>
    public void Retire(Guid retiredBy, DateTime occurredOn)
    {
        if (!IsActive)
            return;

        Guard.NotDefault(retiredBy, nameof(retiredBy));
        IsActive = false;
        RetiredBy = retiredBy;
        RetiredOn = occurredOn.ToUniversalTime();
        UpdatedBy = retiredBy;
        UpdatedOn = occurredOn.ToUniversalTime();
    }
}