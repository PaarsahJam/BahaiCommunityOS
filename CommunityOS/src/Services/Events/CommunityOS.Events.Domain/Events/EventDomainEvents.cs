using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Events.Domain.Events;

public sealed record EventCreatedEvent(string Title) : DomainEvent;

public sealed record EventCancelledEvent(string Reason) : DomainEvent;

public sealed record AttendanceRecordedEvent(Guid MemberId) : DomainEvent;

public sealed record EventRescheduledEvent(DateTime NewStart, DateTime NewEnd) : DomainEvent;
