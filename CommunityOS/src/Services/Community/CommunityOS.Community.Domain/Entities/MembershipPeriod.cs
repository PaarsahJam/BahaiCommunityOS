using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.Entities;

/// <summary>
/// One status period in a membership's history. Each transition appends a
/// period so effective membership and history are both derivable.
/// </summary>
public sealed class MembershipPeriod : Entity<Guid>
{
    private MembershipPeriod() : base(Guid.Empty)
    {
        Status = null!;
        Period = null!;
    }

    private MembershipPeriod(
        Guid id,
        MembershipStatus status,
        EffectivePeriod period) : base(id)
    {
        Status = status;
        Period = period;
    }

    public MembershipStatus Status { get; private set; }
    public EffectivePeriod Period { get; private set; }

    public static MembershipPeriod Create(MembershipStatus status, EffectivePeriod period)
    {
        Guard.NotNull(status, nameof(status));
        Guard.NotNull(period, nameof(period));

        return new MembershipPeriod(Guid.NewGuid(), status, period);
    }
}
