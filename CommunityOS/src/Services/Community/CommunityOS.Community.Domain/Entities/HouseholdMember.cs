using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.Entities;

/// <summary>
/// Membership of a person in a household. A household member is not required
/// to be a family member, and household membership does not imply a shared
/// address.
/// </summary>
public sealed class HouseholdMember : Entity<Guid>
{
    private HouseholdMember() : base(Guid.Empty)
    {
        Role = null!;
        Period = null!;
    }

    private HouseholdMember(
        Guid id,
        Guid personId,
        HouseholdMemberRole role,
        EffectivePeriod period) : base(id)
    {
        PersonId = personId;
        Role = role;
        Period = period;
    }

    public Guid PersonId { get; private set; }
    public HouseholdMemberRole Role { get; private set; }
    public EffectivePeriod Period { get; private set; }

    public static HouseholdMember Create(
        Guid personId,
        HouseholdMemberRole role,
        EffectivePeriod period)
    {
        Guard.NotDefault(personId, nameof(personId));
        Guard.NotNull(role, nameof(role));
        Guard.NotNull(period, nameof(period));

        return new HouseholdMember(Guid.NewGuid(), personId, role, period);
    }

    public void ChangeRole(HouseholdMemberRole role)
    {
        Guard.NotNull(role, nameof(role));
        Role = role;
    }
}
