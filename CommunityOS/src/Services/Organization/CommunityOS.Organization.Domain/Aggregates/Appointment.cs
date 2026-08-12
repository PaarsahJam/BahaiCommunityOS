using CommunityOS.Organization.Domain.Enumerations;
using CommunityOS.Organization.Domain.Events;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Organization.Domain.Aggregates;

/// <summary>
/// An appointment of a person to a role within an organization unit. Person
/// identity is owned by the Community service; the Organization service only
/// carries the stable person id. Appointments are effective-dated and
/// non-overlapping per person / unit / type.
/// </summary>
public sealed class Appointment : AggregateRoot<Guid>
{
    private Appointment() : base(Guid.Empty)
    {
        AppointmentType = null!;
        Period = null!;
        Status = null!;
    }

    private Appointment(
        Guid id,
        Guid personId,
        Guid organizationUnitId,
        string appointmentType,
        EffectivePeriod period,
        Guid? assignedBy,
        string? reason) : base(id)
    {
        PersonId = personId;
        OrganizationUnitId = organizationUnitId;
        AppointmentType = appointmentType;
        Period = period;
        AssignedBy = assignedBy;
        AssignedOn = DateTime.UtcNow;
        Reason = reason;
        Status = AppointmentStatus.Active;
    }

    public Guid PersonId { get; }
    public Guid OrganizationUnitId { get; }
    public string AppointmentType { get; }
    public EffectivePeriod Period { get; }
    public AppointmentStatus Status { get; private set; }
    public Guid? AssignedBy { get; }
    public DateTime AssignedOn { get; }
    public Guid? EndedBy { get; private set; }
    public DateTime? EndedOn { get; private set; }
    public string? Reason { get; }

    public bool IsEnded => Status == AppointmentStatus.Ended;

    public bool IsEffectiveAt(DateTime moment) =>
        !IsEnded && Period.IsEffectiveAt(moment);

    public static Appointment Create(
        Guid personId,
        Guid organizationUnitId,
        string appointmentType,
        EffectivePeriod period,
        Guid? assignedBy,
        string? reason = null)
    {
        Guard.NotDefault(personId, nameof(personId));
        Guard.NotDefault(organizationUnitId, nameof(organizationUnitId));
        Guard.NotNullOrWhiteSpace(appointmentType, nameof(appointmentType));
        Guard.MaxLength(appointmentType, 100, nameof(appointmentType));
        Guard.NotNull(period, nameof(period));
        if (reason is not null) Guard.MaxLength(reason, 500, nameof(reason));

        var appointment = new Appointment(
            Guid.NewGuid(),
            personId,
            organizationUnitId,
            appointmentType.Trim(),
            period,
            assignedBy,
            reason?.Trim());

        appointment.RaiseDomainEvent(new AppointmentAssignedEvent(
            appointment.Id,
            personId,
            organizationUnitId,
            appointment.AppointmentType,
            period.EffectiveFrom,
            period.EffectiveUntil));
        return appointment;
    }

    public void End(Guid? endedBy, string? reason)
    {
        if (IsEnded)
            throw new AppointmentAlreadyEndedException(Id);
        if (reason is not null) Guard.MaxLength(reason, 500, nameof(reason));

        Status = AppointmentStatus.Ended;
        EndedBy = endedBy;
        EndedOn = DateTime.UtcNow;

        RaiseDomainEvent(new AppointmentEndedEvent(Id, PersonId, OrganizationUnitId));
    }
}
