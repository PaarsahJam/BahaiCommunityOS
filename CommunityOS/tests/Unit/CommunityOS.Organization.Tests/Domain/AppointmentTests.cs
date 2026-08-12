using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Enumerations;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.ValueObjects;

namespace CommunityOS.Organization.Tests.Domain;

public class AppointmentTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static EffectivePeriod CurrentPeriod() =>
        EffectivePeriod.Create(Now.AddDays(-30), Now.AddDays(30));

    [Fact]
    public void Create_initializes_active_appointment()
    {
        var appointment = Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Treasurer", CurrentPeriod(), Guid.NewGuid());

        appointment.Status.Should().Be(AppointmentStatus.Active);
        appointment.AssignedOn.Kind.Should().Be(DateTimeKind.Utc);
        appointment.IsEnded.Should().BeFalse();
    }

    [Fact]
    public void IsEffectiveAt_requires_active_status_and_window()
    {
        var appointment = Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Treasurer", CurrentPeriod(), Guid.NewGuid());

        appointment.IsEffectiveAt(Now).Should().BeTrue();
        appointment.IsEffectiveAt(Now.AddDays(-31)).Should().BeFalse();
    }

    [Fact]
    public void End_marks_appointment_ended()
    {
        var appointment = Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Treasurer", CurrentPeriod(), Guid.NewGuid());
        var endedBy = Guid.NewGuid();

        appointment.End(endedBy, "term complete");

        appointment.Status.Should().Be(AppointmentStatus.Ended);
        appointment.EndedBy.Should().Be(endedBy);
        appointment.EndedOn.Should().NotBeNull();
        appointment.IsEffectiveAt(Now).Should().BeFalse();
    }

    [Fact]
    public void End_is_not_repeatable()
    {
        var appointment = Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Treasurer", CurrentPeriod(), Guid.NewGuid());
        appointment.End(Guid.NewGuid(), null);

        var act = () => appointment.End(Guid.NewGuid(), null);

        act.Should().Throw<AppointmentAlreadyEndedException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_appointment_type(string? appointmentType)
    {
        var act = () => Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), appointmentType!, CurrentPeriod(), Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_default_person_or_unit()
    {
        var actPerson = () => Appointment.Create(
            Guid.Empty, Guid.NewGuid(), "Treasurer", CurrentPeriod(), Guid.NewGuid());
        var actUnit = () => Appointment.Create(
            Guid.NewGuid(), Guid.Empty, "Treasurer", CurrentPeriod(), Guid.NewGuid());

        actPerson.Should().Throw<ArgumentException>();
        actUnit.Should().Throw<ArgumentException>();
    }
}
