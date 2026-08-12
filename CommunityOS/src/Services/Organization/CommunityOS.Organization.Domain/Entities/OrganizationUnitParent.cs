using CommunityOS.Organization.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Organization.Domain.Entities;

/// <summary>
/// A single effective-dated parent link of an organization unit. Each unit
/// carries an ordered history of parents so hierarchy changes are auditable
/// and hierarchy can be reconstructed at any point in time.
/// </summary>
public sealed class OrganizationUnitParent : Entity<Guid>
{
    internal OrganizationUnitParent(
        Guid id, Guid? parentId, EffectivePeriod period) : base(id)
    {
        ParentId = parentId;
        Period = period;
    }

    private OrganizationUnitParent() : base(Guid.Empty)
    {
        Period = null!;
    }

    public Guid? ParentId { get; }
    public EffectivePeriod Period { get; }

    public bool IsEffectiveAt(DateTime moment) => Period.IsEffectiveAt(moment);

    public bool IsCurrent(DateTime moment) => Period.IsEffectiveAt(moment);

    public static OrganizationUnitParent Link(Guid? parentId, EffectivePeriod period)
    {
        Guard.NotNull(period, nameof(period));
        return new OrganizationUnitParent(Guid.NewGuid(), parentId, period);
    }
}
