using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Events;
using CommunityOS.Community.Domain.Exceptions;

namespace CommunityOS.Community.Tests.Domain;

public class CommunityEventTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static CommunityEvent CreateEvent() => CommunityEvent.Create(
        "Ridvan Celebration",
        "Nine-day festival",
        Now,
        Now.AddHours(3),
        "UTC",
        "Community Hall",
        false,
        null,
        Guid.NewGuid(),
        Guid.NewGuid(),
        CommunityEventStatus.Scheduled,
        CommunityEventVisibility.Members,
        true,
        50);

    [Fact]
    public void Create_initializes_event()
    {
        var communityEvent = CreateEvent();

        communityEvent.Id.Should().NotBeEmpty();
        communityEvent.Title.Should().Be("Ridvan Celebration");
        communityEvent.RegistrationOpen.Should().BeTrue();
        communityEvent.DomainEvents.Should().ContainSingle(e => e is CommunityEventCreatedEvent);
    }

    [Fact]
    public void Create_rejects_invalid_time_range()
    {
        var act = () => CommunityEvent.Create(
            "Bad Event", null, Now, Now.AddHours(-1), "UTC", null,
            false, null, null, null,
            CommunityEventStatus.Scheduled, CommunityEventVisibility.Public, true, null);

        act.Should().Throw<InvalidTimeRangeException>();
    }

    [Fact]
    public void UpdateDetails_changes_fields_and_raises_event()
    {
        var communityEvent = CreateEvent();

        communityEvent.UpdateDetails(
            "Renamed Event", "New desc", "Other Hall", true,
            "https://example.com/live", CommunityEventVisibility.Public, false);

        communityEvent.Title.Should().Be("Renamed Event");
        communityEvent.IsOnline.Should().BeTrue();
        communityEvent.RegistrationOpen.Should().BeFalse();
        communityEvent.DomainEvents.Should().Contain(e => e is CommunityEventUpdatedEvent);
    }

    [Fact]
    public void Reschedule_changes_window()
    {
        var communityEvent = CreateEvent();
        var newStart = Now.AddDays(1);

        communityEvent.Reschedule(newStart, newStart.AddHours(4));

        communityEvent.StartsAt.Should().Be(newStart);
    }

    [Fact]
    public void Cancel_marks_cancelled_and_is_not_repeatable()
    {
        var communityEvent = CreateEvent();

        communityEvent.Cancel();
        communityEvent.Status.Should().Be(CommunityEventStatus.Cancelled);

        var act = () => communityEvent.Cancel();
        act.Should().Throw<CommunityEventAlreadyCancelledException>();
    }
}
