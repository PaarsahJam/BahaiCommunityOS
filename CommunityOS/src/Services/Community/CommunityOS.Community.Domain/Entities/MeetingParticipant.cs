using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.Entities;

/// <summary>
/// A person's participation in a meeting.
/// </summary>
public sealed class MeetingParticipant : Entity<Guid>
{
    private MeetingParticipant() : base(Guid.Empty)
    {
        Role = null!;
        Attendance = null!;
    }

    private MeetingParticipant(
        Guid id,
        Guid personId,
        string role,
        AttendanceStatus attendance) : base(id)
    {
        PersonId = personId;
        Role = role;
        Attendance = attendance;
    }

    public Guid PersonId { get; private set; }
    public string Role { get; private set; }
    public AttendanceStatus Attendance { get; private set; }

    public static MeetingParticipant Create(Guid personId, string role)
    {
        Guard.NotDefault(personId, nameof(personId));
        Guard.NotNullOrWhiteSpace(role, nameof(role));
        Guard.MaxLength(role, 100, nameof(role));

        return new MeetingParticipant(Guid.NewGuid(), personId, role.Trim(), AttendanceStatus.NotMarked);
    }

    public void MarkAttendance(AttendanceStatus status)
    {
        Guard.NotNull(status, nameof(status));
        Attendance = status;
    }
}
