using CommunityOS.Enrollment.Domain.Entities;
using CommunityOS.Enrollment.Domain.Events;
using CommunityOS.Enrollment.Domain.Exceptions;
using CommunityOS.Enrollment.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Enrollment.Domain.Aggregates;

public sealed class StudyCircle : AggregateRoot<Guid>
{
    private readonly List<Participant> _participants = [];
    private readonly List<Session> _sessions = [];

    public CourseName Course { get; private set; }
    public Guid FacilitatorId { get; private set; }
    public Guid LocalUnitId { get; private set; }
    public ProgressStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public IReadOnlyList<Participant> Participants => _participants.AsReadOnly();
    public IReadOnlyList<Session> Sessions => _sessions.AsReadOnly();

    private StudyCircle(Guid id, CourseName course, Guid facilitatorId, Guid localUnitId) : base(id)
    {
        Course = course;
        FacilitatorId = facilitatorId;
        LocalUnitId = localUnitId;
        Status = ProgressStatus.NotStarted;
        StartedAt = DateTime.UtcNow;
    }

    public static StudyCircle Create(CourseName course, Guid facilitatorId, Guid localUnitId)
    {
        Guard.NotNull(course, nameof(course));
        Guard.NotDefault(facilitatorId, nameof(facilitatorId));
        Guard.NotDefault(localUnitId, nameof(localUnitId));
        var circle = new StudyCircle(Guid.NewGuid(), course, facilitatorId, localUnitId);
        circle.RaiseDomainEvent(new StudyCircleCreatedEvent(circle.Id, course.Value));
        return circle;
    }

    public void EnrollParticipant(Guid memberId)
    {
        if (_participants.Any(p => p.MemberId == memberId))
            throw new ParticipantAlreadyEnrolledException(memberId, Id);
        var participant = Participant.Enroll(memberId);
        _participants.Add(participant);
        RaiseDomainEvent(new ParticipantEnrolledEvent(Id, memberId));
    }

    public void AddSession(Session session)
    {
        Guard.NotNull(session, nameof(session));
        _sessions.Add(session);
    }

    public void CompleteSession(Guid sessionId)
    {
        var session = _sessions.FirstOrDefault(s => s.Id == sessionId)
            ?? throw new SessionNotFoundException(sessionId);
        session.Complete();
        RaiseDomainEvent(new SessionCompletedEvent(Id, sessionId));
        if (_sessions.All(s => s.IsCompleted))
        {
            Status = ProgressStatus.Completed;
            CompletedAt = DateTime.UtcNow;
            RaiseDomainEvent(new StudyCircleCompletedEvent(Id));
        }
    }
}
