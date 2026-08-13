using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Events;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.Aggregates;

/// <summary>
/// A community event (commemoration, devotional gathering, holy day, festival,
/// etc.). Events are Community-owned and distinct from activities and
/// meetings. Scheduling logic is shared with activities via
/// <see cref="ValueObjects.DateTimeRange"/>.
/// </summary>
public sealed class CommunityEvent : AggregateRoot<Guid>
{
    private CommunityEvent() : base(Guid.Empty)
    {
        Title = null!;
        TimeZone = null!;
        Visibility = null!;
        Status = null!;
    }

    private CommunityEvent(
        Guid id,
        string title,
        string? description,
        DateTime startsAt,
        DateTime? endsAt,
        string timeZone,
        string? location,
        bool isOnline,
        string? onlineUrl,
        Guid? organizerPersonId,
        Guid? organizationUnitId,
        CommunityEventStatus status,
        CommunityEventVisibility visibility,
        bool registrationOpen,
        int? capacity) : base(id)
    {
        Title = title;
        Description = description;
        StartsAt = startsAt;
        EndsAt = endsAt;
        TimeZone = timeZone;
        Location = location;
        IsOnline = isOnline;
        OnlineUrl = onlineUrl;
        OrganizerPersonId = organizerPersonId;
        OrganizationUnitId = organizationUnitId;
        Status = status;
        Visibility = visibility;
        RegistrationOpen = registrationOpen;
        Capacity = capacity;
        CreatedOn = DateTime.UtcNow;
    }

    public string Title { get; private set; }
    public string? Description { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime? EndsAt { get; private set; }
    public string TimeZone { get; private set; }
    public string? Location { get; private set; }
    public bool IsOnline { get; private set; }
    public string? OnlineUrl { get; private set; }
    public Guid? OrganizerPersonId { get; private set; }
    public Guid? OrganizationUnitId { get; private set; }
    public CommunityEventStatus Status { get; private set; }
    public CommunityEventVisibility Visibility { get; private set; }
    public bool RegistrationOpen { get; private set; }
    public int? Capacity { get; private set; }
    public DateTime CreatedOn { get; private set; }

    public static CommunityEvent Create(
        string title,
        string? description,
        DateTime startsAt,
        DateTime? endsAt,
        string timeZone,
        string? location,
        bool isOnline,
        string? onlineUrl,
        Guid? organizerPersonId,
        Guid? organizationUnitId,
        CommunityEventStatus status,
        CommunityEventVisibility visibility,
        bool registrationOpen,
        int? capacity)
    {
        Guard.NotNullOrWhiteSpace(title, nameof(title));
        Guard.MaxLength(title, 200, nameof(title));
        Guard.MaxLength(description ?? string.Empty, 2000, nameof(description));
        Guard.NotNullOrWhiteSpace(timeZone, nameof(timeZone));
        Guard.MaxLength(timeZone, 100, nameof(timeZone));
        Guard.MaxLength(location ?? string.Empty, 300, nameof(location));
        Guard.MaxLength(onlineUrl ?? string.Empty, 1000, nameof(onlineUrl));
        Guard.NotNull(status, nameof(status));
        Guard.NotNull(visibility, nameof(visibility));

        var start = startsAt.ToUniversalTime();
        var end = endsAt?.ToUniversalTime();
        if (end is not null && end.Value <= start)
            throw new InvalidTimeRangeException();

        var communityEvent = new CommunityEvent(
            Guid.NewGuid(),
            title.Trim(),
            TrimBlank(description),
            start,
            end,
            timeZone.Trim(),
            TrimBlank(location),
            isOnline,
            TrimBlank(onlineUrl),
            organizerPersonId,
            organizationUnitId,
            status,
            visibility,
            registrationOpen,
            capacity);

        communityEvent.RaiseDomainEvent(new CommunityEventCreatedEvent(
            communityEvent.Id,
            communityEvent.Title,
            communityEvent.StartsAt,
            communityEvent.OrganizationUnitId,
            communityEvent.Status.Name,
            CommunityEvent.OccurredAt()));
        return communityEvent;
    }

    public void UpdateDetails(
        string title,
        string? description,
        string? location,
        bool isOnline,
        string? onlineUrl,
        CommunityEventVisibility visibility,
        bool registrationOpen)
    {
        Guard.NotNullOrWhiteSpace(title, nameof(title));
        Guard.MaxLength(title, 200, nameof(title));
        Guard.MaxLength(description ?? string.Empty, 2000, nameof(description));
        Guard.MaxLength(location ?? string.Empty, 300, nameof(location));
        Guard.MaxLength(onlineUrl ?? string.Empty, 1000, nameof(onlineUrl));
        Guard.NotNull(visibility, nameof(visibility));

        Title = title.Trim();
        Description = TrimBlank(description);
        Location = TrimBlank(location);
        IsOnline = isOnline;
        OnlineUrl = TrimBlank(onlineUrl);
        Visibility = visibility;
        RegistrationOpen = registrationOpen;

        RaiseDomainEvent(new CommunityEventUpdatedEvent(Id, StartsAt, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    public void Reschedule(DateTime startsAt, DateTime? endsAt)
    {
        var start = startsAt.ToUniversalTime();
        var end = endsAt?.ToUniversalTime();
        if (end is not null && end.Value <= start)
            throw new InvalidTimeRangeException();

        StartsAt = start;
        EndsAt = end;

        RaiseDomainEvent(new CommunityEventUpdatedEvent(Id, StartsAt, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    public void ChangeOrganizationContext(Guid? organizationUnitId)
    {
        OrganizationUnitId = organizationUnitId;

        RaiseDomainEvent(new CommunityEventUpdatedEvent(Id, StartsAt, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    public void ChangeStatus(CommunityEventStatus status)
    {
        Guard.NotNull(status, nameof(status));
        Status = status;

        RaiseDomainEvent(new CommunityEventUpdatedEvent(Id, StartsAt, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    public void Cancel()
    {
        if (Status == CommunityEventStatus.Cancelled)
            throw new CommunityEventAlreadyCancelledException(Id);

        Status = CommunityEventStatus.Cancelled;
        RaiseDomainEvent(new CommunityEventUpdatedEvent(Id, StartsAt, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    private static DateTime OccurredAt() => DateTime.UtcNow;

    private static string? TrimBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
