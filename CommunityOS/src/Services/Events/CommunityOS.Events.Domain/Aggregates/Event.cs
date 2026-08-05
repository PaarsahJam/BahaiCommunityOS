using CommunityOS.Events.Domain.Entities;
using CommunityOS.Events.Domain.Events;
using CommunityOS.Events.Domain.Exceptions;
using CommunityOS.Events.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Events.Domain.Aggregates;

public sealed class Event : AggregateRoot<Guid>
{
    private readonly List<Attendance> _attendances = [];

    public EventTitle Title { get; private set; }
    public string? Description { get; private set; }
    public DateTimeRange Schedule { get; private set; }
    public Location Location { get; private set; }
    public Capacity Capacity { get; private set; }
    public Guid LocalUnitId { get; private set; }
    public Guid OrganiserId { get; private set; }
    public bool IsCancelled { get; private set; }

    public IReadOnlyList<Attendance> Attendances => _attendances.AsReadOnly();

    private Event(Guid id, EventTitle title, string? description, DateTimeRange schedule,
        Location location, Capacity capacity, Guid localUnitId, Guid organiserId) : base(id)
    {
        Title = title;
        Description = description;
        Schedule = schedule;
        Location = location;
        Capacity = capacity;
        LocalUnitId = localUnitId;
        OrganiserId = organiserId;
    }

    public static Event Create(EventTitle title, DateTimeRange schedule, Location location,
        Capacity capacity, Guid localUnitId, Guid organiserId, string? description = null)
    {
        Guard.NotNull(title, nameof(title));
        Guard.NotNull(schedule, nameof(schedule));
        Guard.NotNull(location, nameof(location));
        Guard.NotNull(capacity, nameof(capacity));
        Guard.NotDefault(localUnitId, nameof(localUnitId));
        Guard.NotDefault(organiserId, nameof(organiserId));
        var @event = new Event(Guid.NewGuid(), title, description, schedule,
            location, capacity, localUnitId, organiserId);
        @event.RaiseDomainEvent(new EventCreatedEvent(@event.Id, title.Value));
        return @event;
    }

    public void RecordAttendance(Guid memberId)
    {
        if (IsCancelled) throw new EventAlreadyCancelledException(Id);
        if (!Capacity.CanAccommodate(_attendances.Count + 1))
            throw new EventCapacityExceededException(Id);
        if (_attendances.Any(a => a.MemberId == memberId)) return;
        var attendance = Attendance.Record(memberId);
        _attendances.Add(attendance);
        RaiseDomainEvent(new AttendanceRecordedEvent(Id, memberId));
    }

    public void Cancel(string reason)
    {
        Guard.NotNullOrWhiteSpace(reason, nameof(reason));
        if (IsCancelled) throw new EventAlreadyCancelledException(Id);
        IsCancelled = true;
        RaiseDomainEvent(new EventCancelledEvent(Id, reason));
    }

    public void Reschedule(DateTimeRange newSchedule)
    {
        Guard.NotNull(newSchedule, nameof(newSchedule));
        Schedule = newSchedule;
        RaiseDomainEvent(new EventRescheduledEvent(Id, newSchedule.Start, newSchedule.End));
    }
}
