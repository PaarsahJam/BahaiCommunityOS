using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Knowledge.Domain.Aggregates;

/// <summary>
/// A Knowledge-side read-model of an organization unit fact owned by the
/// Organization service. Kept in sync by consuming Organization integration
/// events so Knowledge can scope questions, answers, and discussions to a
/// jurisdiction without owning the organizational hierarchy. See ADR-016.
/// </summary>
public sealed class OrganizationUnitReference : AggregateRoot<Guid>
{
    private OrganizationUnitReference() : base(Guid.Empty)
    {
        Name = null!;
        UnitType = null!;
    }

    private OrganizationUnitReference(
        Guid organizationUnitId,
        Guid organizationId,
        string name,
        string unitType,
        Guid? parentId,
        DateTime occurredOn) : base(organizationUnitId)
    {
        OrganizationId = organizationId;
        Name = name;
        UnitType = unitType;
        ParentId = parentId;
        LastSeenOn = occurredOn;
    }

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; }
    public string UnitType { get; private set; }
    public Guid? ParentId { get; private set; }
    public DateTime LastSeenOn { get; private set; }

    public static OrganizationUnitReference Create(
        Guid organizationUnitId,
        Guid organizationId,
        string name,
        string unitType,
        Guid? parentId,
        DateTime occurredOn)
    {
        Guard.NotDefault(organizationUnitId, nameof(organizationUnitId));
        Guard.NotDefault(organizationId, nameof(organizationId));
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, 200, nameof(name));
        Guard.NotNullOrWhiteSpace(unitType, nameof(unitType));
        Guard.MaxLength(unitType, 100, nameof(unitType));

        return new OrganizationUnitReference(
            organizationUnitId,
            organizationId,
            name.Trim(),
            unitType.Trim(),
            parentId,
            occurredOn.ToUniversalTime());
    }

    public void Sync(string name, string unitType, Guid? parentId, DateTime occurredOn)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, 200, nameof(name));
        Guard.NotNullOrWhiteSpace(unitType, nameof(unitType));
        Guard.MaxLength(unitType, 100, nameof(unitType));

        Name = name.Trim();
        UnitType = unitType.Trim();
        ParentId = parentId;
        LastSeenOn = occurredOn.ToUniversalTime();
    }
}