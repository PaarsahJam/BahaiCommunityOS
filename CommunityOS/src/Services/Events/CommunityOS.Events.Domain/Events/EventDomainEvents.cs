using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Events.Domain.Events;

public sealed record EventCreatedEvent(Guid EventId, string Title) : DomainEvent;

public sealed record EventCancelledEvent(Guid EventId, string Reason) : DomainEvent;

public sealed record AttendanceRecordedEvent(Guid EventId, Guid MemberId) : DomainEvent;

public sealed record EventRescheduledEvent(Guid EventId, DateTime NewStart, DateTime NewEnd) : DomainEvent;
