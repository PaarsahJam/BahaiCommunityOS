using CommunityOS.Community.Domain.Entities;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Events;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.Aggregates;

/// <summary>
/// Community membership of a person. Membership is a Community concern and is
/// never represented by an authorization role or an organization appointment.
/// A lifecycle (status + effective window) with history supports effective
/// membership queries and preserves history.
/// </summary>
public sealed class Membership : AggregateRoot<Guid>
{
    private readonly List<MembershipPeriod> _periods = [];

    private Membership() : base(Guid.Empty)
    {
        Status = null!;
        Period = null!;
    }

    private Membership(
        Guid id,
        Guid personId,
        MembershipStatus status,
        EffectivePeriod period) : base(id)
    {
        PersonId = personId;
        Status = status;
        Period = period;
        _periods.Add(MembershipPeriod.Create(status, period));
    }

    public Guid PersonId { get; private set; }
    public MembershipStatus Status { get; private set; }
    public EffectivePeriod Period { get; private set; }
    public DateTime? WithdrawnOn { get; private set; }

    public IReadOnlyList<MembershipPeriod> PeriodHistory => _periods.AsReadOnly();

    public static Membership Create(Guid personId, MembershipStatus status, EffectivePeriod period)
    {
        Guard.NotDefault(personId, nameof(personId));
        Guard.NotNull(status, nameof(status));
        Guard.NotNull(period, nameof(period));

        var membership = new Membership(Guid.NewGuid(), personId, status, period);
        membership.RaiseDomainEvent(new MembershipChangedEvent(
            membership.Id,
            membership.PersonId,
            membership.Status.Name,
            membership.Period.EffectiveFrom,
            membership.Period.EffectiveUntil,
            DateTime.UtcNow));
        return membership;
    }

    /// <summary>
    /// True when the membership is currently effective with the given status.
    /// </summary>
    public bool IsEffectiveAt(MembershipStatus status, DateTime moment) =>
        Status == status && Period.IsEffectiveAt(moment);

    /// <summary>
    /// Transitions the membership to a new status, appending the previous
    /// period to history.
    /// </summary>
    public void ChangeStatus(MembershipStatus newStatus, DateTime? effectiveFrom = null)
    {
        Guard.NotNull(newStatus, nameof(newStatus));

        if (Status == newStatus)
            throw new MembershipStatusUnchangedException(Id, newStatus.Name);

        var from = effectiveFrom?.ToUniversalTime() ?? DateTime.UtcNow;
        if (from <= Period.EffectiveFrom)
            throw new InvalidEffectivePeriodException();

        _periods.Add(MembershipPeriod.Create(Status, EffectivePeriod.Create(Period.EffectiveFrom, from)));

        Status = newStatus;
        Period = EffectivePeriod.Create(from);
        WithdrawnOn = newStatus == MembershipStatus.Withdrawn ? from : WithdrawnOn;

        RaiseDomainEvent(new MembershipChangedEvent(
            Id,
            PersonId,
            Status.Name,
            Period.EffectiveFrom,
            Period.EffectiveUntil,
            DateTime.UtcNow));
    }
}
