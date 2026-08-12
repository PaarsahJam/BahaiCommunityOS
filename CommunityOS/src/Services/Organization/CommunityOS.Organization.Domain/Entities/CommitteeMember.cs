using CommunityOS.Organization.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Organization.Domain.Entities;

/// <summary>
/// A member of a committee with an effective-dated role within it.
/// </summary>
public sealed class CommitteeMember : Entity<Guid>
{
    internal CommitteeMember(
        Guid id, Guid personId, string roleCode, EffectivePeriod period) : base(id)
    {
        PersonId = personId;
        RoleCode = roleCode;
        Period = period;
    }

    private CommitteeMember() : base(Guid.Empty)
    {
        RoleCode = null!;
        Period = null!;
    }

    public Guid PersonId { get; }
    public string RoleCode { get; }
    public EffectivePeriod Period { get; }

    public bool IsEffectiveAt(DateTime moment) => Period.IsEffectiveAt(moment);

    public static CommitteeMember Add(
        Guid personId, string roleCode, EffectivePeriod period)
    {
        Guard.NotDefault(personId, nameof(personId));
        Guard.NotNullOrWhiteSpace(roleCode, nameof(roleCode));
        Guard.NotNull(period, nameof(period));
        return new CommitteeMember(Guid.NewGuid(), personId, roleCode.Trim(), period);
    }
}
