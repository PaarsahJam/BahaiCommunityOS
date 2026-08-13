using CommunityOS.Community.Domain.Entities;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Events;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.Aggregates;

/// <summary>
/// A meeting supporting the community workflow: scheduled time, organization
/// context, participants, agenda, action items and draft minutes. Minutes are
/// operational drafts — formal records belong to the future Records service.
/// </summary>
public sealed class Meeting : AggregateRoot<Guid>
{
    private readonly List<MeetingParticipant> _participants = [];
    private readonly List<MeetingAgendaItem> _agendaItems = [];
    private readonly List<MeetingAction> _actions = [];

    private Meeting() : base(Guid.Empty)
    {
        Title = null!;
        TimeZone = null!;
        Visibility = null!;
        Status = null!;
    }

    private Meeting(
        Guid id,
        string title,
        string? description,
        DateTime startsAt,
        DateTime? endsAt,
        string timeZone,
        string? location,
        Guid? organizerPersonId,
        Guid? organizationUnitId,
        MeetingStatus status,
        MeetingVisibility visibility) : base(id)
    {
        Title = title;
        Description = description;
        StartsAt = startsAt;
        EndsAt = endsAt;
        TimeZone = timeZone;
        Location = location;
        OrganizerPersonId = organizerPersonId;
        OrganizationUnitId = organizationUnitId;
        Status = status;
        Visibility = visibility;
        CreatedOn = DateTime.UtcNow;
    }

    public string Title { get; private set; }
    public string? Description { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime? EndsAt { get; private set; }
    public string TimeZone { get; private set; }
    public string? Location { get; private set; }
    public Guid? OrganizerPersonId { get; private set; }
    public Guid? OrganizationUnitId { get; private set; }
    public MeetingStatus Status { get; private set; }
    public MeetingVisibility Visibility { get; private set; }
    public string? Minutes { get; private set; }
    public DateTime CreatedOn { get; private set; }

    public IReadOnlyList<MeetingParticipant> Participants => _participants.AsReadOnly();
    public IReadOnlyList<MeetingAgendaItem> AgendaItems => _agendaItems.AsReadOnly();
    public IReadOnlyList<MeetingAction> Actions => _actions.AsReadOnly();

    public static Meeting Create(
        string title,
        string? description,
        DateTime startsAt,
        DateTime? endsAt,
        string timeZone,
        string? location,
        Guid? organizerPersonId,
        Guid? organizationUnitId,
        MeetingStatus status,
        MeetingVisibility visibility)
    {
        Guard.NotNullOrWhiteSpace(title, nameof(title));
        Guard.MaxLength(title, 200, nameof(title));
        Guard.MaxLength(description ?? string.Empty, 2000, nameof(description));
        Guard.NotNullOrWhiteSpace(timeZone, nameof(timeZone));
        Guard.MaxLength(timeZone, 100, nameof(timeZone));
        Guard.MaxLength(location ?? string.Empty, 300, nameof(location));
        Guard.NotNull(status, nameof(status));
        Guard.NotNull(visibility, nameof(visibility));

        var start = startsAt.ToUniversalTime();
        var end = endsAt?.ToUniversalTime();
        if (end is not null && end.Value <= start)
            throw new InvalidTimeRangeException();

        var meeting = new Meeting(
            Guid.NewGuid(),
            title.Trim(),
            TrimBlank(description),
            start,
            end,
            timeZone.Trim(),
            TrimBlank(location),
            organizerPersonId,
            organizationUnitId,
            status,
            visibility);

        meeting.RaiseDomainEvent(new MeetingCreatedEvent(
            meeting.Id,
            meeting.Title,
            meeting.StartsAt,
            meeting.OrganizationUnitId,
            meeting.Status.Name,
            Meeting.OccurredAt()));
        return meeting;
    }

    public void UpdateDetails(
        string title,
        string? description,
        string? location,
        MeetingVisibility visibility)
    {
        Guard.NotNullOrWhiteSpace(title, nameof(title));
        Guard.MaxLength(title, 200, nameof(title));
        Guard.MaxLength(description ?? string.Empty, 2000, nameof(description));
        Guard.MaxLength(location ?? string.Empty, 300, nameof(location));
        Guard.NotNull(visibility, nameof(visibility));

        Title = title.Trim();
        Description = TrimBlank(description);
        Location = TrimBlank(location);
        Visibility = visibility;

        RaiseDomainEvent(new MeetingUpdatedEvent(Id, StartsAt, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    public void Reschedule(DateTime startsAt, DateTime? endsAt)
    {
        var start = startsAt.ToUniversalTime();
        var end = endsAt?.ToUniversalTime();
        if (end is not null && end.Value <= start)
            throw new InvalidTimeRangeException();

        StartsAt = start;
        EndsAt = end;

        RaiseDomainEvent(new MeetingUpdatedEvent(Id, StartsAt, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    public void ChangeOrganizationContext(Guid? organizationUnitId)
    {
        OrganizationUnitId = organizationUnitId;

        RaiseDomainEvent(new MeetingUpdatedEvent(Id, StartsAt, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    public void AddParticipant(Guid personId, string role)
    {
        Guard.NotDefault(personId, nameof(personId));

        if (_participants.Any(p => p.PersonId == personId))
            throw new DuplicateMeetingParticipantException(Id, personId);

        _participants.Add(MeetingParticipant.Create(personId, role));
        RaiseDomainEvent(new MeetingUpdatedEvent(Id, StartsAt, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    public void RemoveParticipant(Guid personId)
    {
        var participant = _participants.FirstOrDefault(p => p.PersonId == personId);
        if (participant is null)
            throw new MeetingParticipantNotFoundException(Id, personId);

        _participants.Remove(participant);
        RaiseDomainEvent(new MeetingUpdatedEvent(Id, StartsAt, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    public void RecordAttendance(Guid personId, AttendanceStatus status)
    {
        Guard.NotNull(status, nameof(status));

        var participant = _participants.FirstOrDefault(p => p.PersonId == personId);
        if (participant is null)
            throw new MeetingParticipantNotFoundException(Id, personId);

        participant.MarkAttendance(status);
        RaiseDomainEvent(new MeetingUpdatedEvent(Id, StartsAt, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    public void AddAgendaItem(string title, string? description, int order)
    {
        var item = MeetingAgendaItem.Create(title, description, order);
        _agendaItems.Add(item);
        RaiseDomainEvent(new MeetingUpdatedEvent(Id, StartsAt, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    public void CompleteAgendaItem(Guid agendaItemId)
    {
        var item = _agendaItems.FirstOrDefault(i => i.Id == agendaItemId)
            ?? throw new MeetingAgendaItemNotFoundException(Id, agendaItemId);

        item.Complete();
        RaiseDomainEvent(new MeetingUpdatedEvent(Id, StartsAt, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    public void AddAction(string description, Guid? assigneePersonId, DateTime? dueDate)
    {
        _actions.Add(MeetingAction.Create(description, assigneePersonId, dueDate));
        RaiseDomainEvent(new MeetingUpdatedEvent(Id, StartsAt, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    public void CompleteAction(Guid actionId)
    {
        var action = _actions.FirstOrDefault(a => a.Id == actionId)
            ?? throw new MeetingActionNotFoundException(Id, actionId);

        action.Complete();
        RaiseDomainEvent(new MeetingUpdatedEvent(Id, StartsAt, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    /// <summary>
    /// Records operational minutes. Minutes are drafts for the community
    /// workflow; formally declared records belong to the Records service.
    /// </summary>
    public void RecordMinutes(string minutes)
    {
        Guard.MaxLength(minutes, 20000, nameof(minutes));
        Minutes = minutes;
        Status = MeetingStatus.Completed;

        RaiseDomainEvent(new MeetingRecordedEvent(Id, OrganizationUnitId, OccurredAt()));
    }

    public void ChangeStatus(MeetingStatus status)
    {
        Guard.NotNull(status, nameof(status));
        Status = status;

        RaiseDomainEvent(new MeetingUpdatedEvent(Id, StartsAt, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    public void Cancel()
    {
        if (Status == MeetingStatus.Cancelled)
            throw new MeetingAlreadyCancelledException(Id);

        Status = MeetingStatus.Cancelled;
        RaiseDomainEvent(new MeetingUpdatedEvent(Id, StartsAt, OrganizationUnitId, Status.Name, OccurredAt()));
    }

    private static DateTime OccurredAt() => DateTime.UtcNow;

    private static string? TrimBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
