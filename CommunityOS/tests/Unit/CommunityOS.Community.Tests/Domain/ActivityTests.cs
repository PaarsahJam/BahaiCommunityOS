using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Events;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.ValueObjects;

namespace CommunityOS.Community.Tests.Domain;

public class ActivityTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static Activity CreateActivity() => Activity.Create(
        "Study Circle",
        "Ruhi Book 1",
        "study_circle",
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Community Hall",
        false,
        null,
        DateTimeRange.Create(Now, Now.AddHours(2)),
        ActivityVisibility.Members,
        ActivityStatus.Active,
        12);

    [Fact]
    public void Create_initializes_activity()
    {
        var activity = CreateActivity();

        activity.Id.Should().NotBeEmpty();
        activity.Title.Should().Be("Study Circle");
        activity.Status.Should().Be(ActivityStatus.Active);
        activity.DomainEvents.Should().ContainSingle(e => e is ActivityCreatedEvent);
    }

    [Fact]
    public void Create_rejects_blank_title()
    {
        var act = () => Activity.Create(
            "   ", null, null, null, null, null, false, null,
            DateTimeRange.Create(Now, Now.AddHours(2)),
            ActivityVisibility.Public, ActivityStatus.Planned, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_changes_fields_and_raises_event()
    {
        var activity = CreateActivity();

        activity.UpdateDetails(
            "Devotional Gathering", "New description", "devotional",
            null, "Other Hall", true, "https://example.com/live",
            ActivityVisibility.Public);

        activity.Title.Should().Be("Devotional Gathering");
        activity.Category.Should().Be("devotional");
        activity.IsOnline.Should().BeTrue();
        activity.DomainEvents.Should().Contain(e => e is ActivityUpdatedEvent);
    }

    [Fact]
    public void ChangeSchedule_replaces_schedule()
    {
        var activity = CreateActivity();
        var newSchedule = DateTimeRange.Create(Now.AddDays(1), Now.AddDays(1).AddHours(3));

        activity.ChangeSchedule(newSchedule);

        activity.Schedule.Should().Be(newSchedule);
    }

    [Fact]
    public void ChangeOrganizationContext_updates_unit()
    {
        var activity = CreateActivity();
        var unit = Guid.NewGuid();

        activity.ChangeOrganizationContext(unit);

        activity.OrganizationUnitId.Should().Be(unit);
    }

    [Fact]
    public void Cancel_marks_cancelled_and_is_not_repeatable()
    {
        var activity = CreateActivity();

        activity.Cancel();
        activity.Status.Should().Be(ActivityStatus.Cancelled);

        var act = () => activity.Cancel();
        act.Should().Throw<ActivityAlreadyCancelledException>();
    }
}
