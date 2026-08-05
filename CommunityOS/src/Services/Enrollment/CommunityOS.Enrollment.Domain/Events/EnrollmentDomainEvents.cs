using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Enrollment.Domain.Events;

public sealed record StudyCircleCreatedEvent(Guid StudyCircleId, string CourseName) : DomainEvent;

public sealed record ParticipantEnrolledEvent(Guid StudyCircleId, Guid MemberId) : DomainEvent;

public sealed record SessionCompletedEvent(Guid StudyCircleId, Guid SessionId) : DomainEvent;

public sealed record StudyCircleCompletedEvent(Guid StudyCircleId) : DomainEvent;
