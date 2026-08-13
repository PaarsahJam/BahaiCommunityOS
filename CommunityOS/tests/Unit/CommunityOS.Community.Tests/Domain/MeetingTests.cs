using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Events;
using CommunityOS.Community.Domain.Exceptions;

namespace CommunityOS.Community.Tests.Domain;

public class MeetingTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static Meeting CreateMeeting() => Meeting.Create(
        "Nineteen Day Feast",
        "Monthly gathering",
        Now,
        Now.AddHours(1),
        "UTC",
        "Community Hall",
        Guid.NewGuid(),
        Guid.NewGuid(),
        MeetingStatus.Scheduled,
        MeetingVisibility.Members);

    [Fact]
    public void Create_initializes_meeting()
    {
        var meeting = CreateMeeting();

        meeting.Id.Should().NotBeEmpty();
        meeting.Status.Should().Be(MeetingStatus.Scheduled);
        meeting.DomainEvents.Should().ContainSingle(e => e is MeetingCreatedEvent);
    }

    [Fact]
    public void Create_rejects_invalid_time_range()
    {
        var act = () => Meeting.Create(
            "Bad", null, Now, Now.AddMinutes(-5), "UTC", null,
            null, null, MeetingStatus.Scheduled, MeetingVisibility.Public);

        act.Should().Throw<InvalidTimeRangeException>();
    }

    [Fact]
    public void AddParticipant_appends_and_rejects_duplicate()
    {
        var meeting = CreateMeeting();
        var personId = Guid.NewGuid();

        meeting.AddParticipant(personId, "convener");
        meeting.Participants.Should().ContainSingle(p => p.PersonId == personId);

        var act = () => meeting.AddParticipant(personId, "member");
        act.Should().Throw<DuplicateMeetingParticipantException>();
    }

    [Fact]
    public void RecordAttendance_updates_participant_status()
    {
        var meeting = CreateMeeting();
        var personId = Guid.NewGuid();
        meeting.AddParticipant(personId, "member");

        meeting.RecordAttendance(personId, AttendanceStatus.Attended);

        meeting.Participants.Single(p => p.PersonId == personId)
            .Attendance.Should().Be(AttendanceStatus.Attended);
    }

    [Fact]
    public void RecordAttendance_throws_for_non_participant()
    {
        var meeting = CreateMeeting();

        var act = () => meeting.RecordAttendance(Guid.NewGuid(), AttendanceStatus.Attended);

        act.Should().Throw<MeetingParticipantNotFoundException>();
    }

    [Fact]
    public void AddAgendaItem_and_complete()
    {
        var meeting = CreateMeeting();

        meeting.AddAgendaItem("Consultation", "Main topic", 1);
        var item = meeting.AgendaItems.Single();
        item.IsCompleted.Should().BeFalse();

        meeting.CompleteAgendaItem(item.Id);
        meeting.AgendaItems.Single(i => i.Id == item.Id).IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void AddAction_and_complete()
    {
        var meeting = CreateMeeting();
        var assignee = Guid.NewGuid();

        meeting.AddAction("Prepare minutes", assignee, Now.AddDays(3));
        var action = meeting.Actions.Single();
        action.IsCompleted.Should().BeFalse();

        meeting.CompleteAction(action.Id);
        meeting.Actions.Single(a => a.Id == action.Id).IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void RecordMinutes_marks_meeting_completed()
    {
        var meeting = CreateMeeting();

        meeting.RecordMinutes("Approved as draft.");

        meeting.Minutes.Should().Be("Approved as draft.");
        meeting.Status.Should().Be(MeetingStatus.Completed);
        meeting.DomainEvents.Should().Contain(e => e is MeetingRecordedEvent);
    }

    [Fact]
    public void Cancel_marks_cancelled_and_is_not_repeatable()
    {
        var meeting = CreateMeeting();

        meeting.Cancel();
        meeting.Status.Should().Be(MeetingStatus.Cancelled);

        var act = () => meeting.Cancel();
        act.Should().Throw<MeetingAlreadyCancelledException>();
    }
}
