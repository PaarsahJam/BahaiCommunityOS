namespace CommunityOS.Enrollment.Domain.Exceptions;

public sealed class StudyCircleNotFoundException(Guid id)
    : Exception($"Study circle '{id}' was not found.");

public sealed class ParticipantAlreadyEnrolledException(Guid memberId, Guid circleId)
    : Exception($"Member '{memberId}' is already enrolled in study circle '{circleId}'.");

public sealed class SessionNotFoundException(Guid sessionId)
    : Exception($"Session '{sessionId}' was not found.");
