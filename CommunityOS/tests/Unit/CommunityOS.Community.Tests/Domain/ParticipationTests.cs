using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Events;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.ValueObjects;

namespace CommunityOS.Community.Tests.Domain;

public class ParticipationTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static Participation CreateParticipation() => Participation.Create(
        Guid.NewGuid(),
        ParticipationTargetType.Activity,
        Guid.NewGuid(),
        "facilitator",
        ParticipationStatus.Registered,
        EffectivePeriod.Create(Now));

    [Fact]
    public void Create_initializes_participation()
    {
        var participation = CreateParticipation();

        participation.Id.Should().NotBeEmpty();
        participation.Status.Should().Be(ParticipationStatus.Registered);
        participation.RecordedOn.Should().BeCloseTo(Now, TimeSpan.FromSeconds(5));
        participation.DomainEvents.Should().ContainSingle(e => e is ParticipationRecordedEvent);
    }

    [Fact]
    public void UpdateStatus_transitions_status()
    {
        var participation = CreateParticipation();

        participation.UpdateStatus(ParticipationStatus.Attended);

        participation.Status.Should().Be(ParticipationStatus.Attended);
    }

    [Fact]
    public void MarkAttended_sets_attended()
    {
        var participation = CreateParticipation();

        participation.MarkAttended();

        participation.Status.Should().Be(ParticipationStatus.Attended);
    }

    [Fact]
    public void UpdateStatus_rejects_change_after_cancel()
    {
        var participation = CreateParticipation();
        participation.Cancel();

        var act = () => participation.UpdateStatus(ParticipationStatus.Attended);

        act.Should().Throw<ParticipationAlreadyCancelledException>();
    }

    [Fact]
    public void Cancel_is_not_repeatable()
    {
        var participation = CreateParticipation();
        participation.Cancel();

        var act = () => participation.Cancel();

        act.Should().Throw<ParticipationAlreadyCancelledException>();
    }

    [Fact]
    public void Create_rejects_blank_target_id()
    {
        var act = () => Participation.Create(
            Guid.NewGuid(), ParticipationTargetType.Event, Guid.Empty,
            null, ParticipationStatus.Registered, EffectivePeriod.Create(Now));

        act.Should().Throw<ArgumentException>();
    }
}
