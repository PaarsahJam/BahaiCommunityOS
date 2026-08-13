using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Events;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.Aggregates;

/// <summary>
/// A community activity (gathering, study circle, devotional meeting, service
/// project, etc.). Categories are configurable rather than a closed list. The
/// activity references organizational context through an organization unit id
/// (a reference to the Organization read model — never owned by Community).
/// </summary>
public sealed class Activity : AggregateRoot<Guid>
{
    private Activity() : base(Guid.Empty)
    {
        Title = null!;
        Visibility = null!;
        Status = null!;
        Schedule = null!;
    }

    private Activity(
        Guid id,
        string title,
        string? description,
        string? category,
        Guid? organizerPersonId,
        Guid? organizationUnitId,
        string? location,
        bool isOnline,
        string? onlineUrl,
        DateTimeRange schedule,
        ActivityVisibility visibility,
        ActivityStatus status,
        int? capacity) : base(id)
    {
        Title = title;
        Description = description;
        Category = category;
        OrganizerPersonId = organizerPersonId;
        OrganizationUnitId = organizationUnitId;
        Location = location;
        IsOnline = isOnline;
        OnlineUrl = onlineUrl;
        Schedule = schedule;
        Visibility = visibility;
        Status = status;
        Capacity = capacity;
        CreatedOn = DateTime.UtcNow;
    }

    public string Title { get; private set; }
    public string? Description { get; private set; }
    public string? Category { get; private set; }
    public Guid? OrganizerPersonId { get; private set; }
    public Guid? OrganizationUnitId { get; private set; }
    public string? Location { get; private set; }
    public bool IsOnline { get; private set; }
    public string? OnlineUrl { get; private set; }
    public DateTimeRange Schedule { get; private set; }
    public ActivityVisibility Visibility { get; private set; }
    public ActivityStatus Status { get; private set; }
    public int? Capacity { get; private set; }
    public DateTime CreatedOn { get; private set; }

    public static Activity Create(
        string title,
        string? description,
        string? category,
        Guid? organizerPersonId,
        Guid? organizationUnitId,
        string? location,
        bool isOnline,
        string? onlineUrl,
        DateTimeRange schedule,
        ActivityVisibility visibility,
        ActivityStatus status,
        int? capacity)
    {
        Guard.NotNullOrWhiteSpace(title, nameof(title));
        Guard.MaxLength(title, 200, nameof(title));
        Guard.MaxLength(description ?? string.Empty, 2000, nameof(description));
        Guard.MaxLength(category ?? string.Empty, 100, nameof(category));
        Guard.MaxLength(location ?? string.Empty, 300, nameof(location));
        Guard.MaxLength(onlineUrl ?? string.Empty, 1000, nameof(onlineUrl));
        Guard.NotNull(schedule, nameof(schedule));
        Guard.NotNull(visibility, nameof(visibility));
        Guard.NotNull(status, nameof(status));

        var activity = new Activity(
            Guid.NewGuid(),
            title.Trim(),
            TrimBlank(description),
            TrimBlank(category),
            organizerPersonId,
            organizationUnitId,
            TrimBlank(location),
            isOnline,
            TrimBlank(onlineUrl),
            schedule,
            visibility,
            status,
            capacity);

        activity.RaiseDomainEvent(new ActivityCreatedEvent(
            activity.Id,
            activity.OrganizationUnitId,
            activity.Status.Name,
            Activity.OccurredAt()));
        return activity;
    }

    public void UpdateDetails(
        string title,
        string? description,
        string? category,
        Guid? organizerPersonId,
        string? location,
        bool isOnline,
        string? onlineUrl,
        ActivityVisibility visibility)
    {
        Guard.NotNullOrWhiteSpace(title, nameof(title));
        Guard.MaxLength(title, 200, nameof(title));
        Guard.MaxLength(description ?? string.Empty, 2000, nameof(description));
        Guard.MaxLength(category ?? string.Empty, 100, nameof(category));
        Guard.MaxLength(location ?? string.Empty, 300, nameof(location));
        Guard.MaxLength(onlineUrl ?? string.Empty, 1000, nameof(onlineUrl));
        Guard.NotNull(visibility, nameof(visibility));

        Title = title.Trim();
        Description = TrimBlank(description);
        Category = TrimBlank(category);
        OrganizerPersonId = organizerPersonId;
        Location = TrimBlank(location);
        IsOnline = isOnline;
        OnlineUrl = TrimBlank(onlineUrl);
        Visibility = visibility;

        RaiseDomainEvent(new ActivityUpdatedEvent(Id, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    public void ChangeSchedule(DateTimeRange schedule)
    {
        Guard.NotNull(schedule, nameof(schedule));
        Schedule = schedule;

        RaiseDomainEvent(new ActivityUpdatedEvent(Id, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    public void ChangeOrganizationContext(Guid? organizationUnitId)
    {
        OrganizationUnitId = organizationUnitId;

        RaiseDomainEvent(new ActivityUpdatedEvent(Id, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    public void ChangeStatus(ActivityStatus status)
    {
        Guard.NotNull(status, nameof(status));
        Status = status;

        RaiseDomainEvent(new ActivityUpdatedEvent(Id, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    public void Cancel()
    {
        if (Status == ActivityStatus.Cancelled)
            throw new ActivityAlreadyCancelledException(Id);

        Status = ActivityStatus.Cancelled;
        RaiseDomainEvent(new ActivityUpdatedEvent(Id, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    private static DateTime OccurredAt() => DateTime.UtcNow;

    private static string? TrimBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
