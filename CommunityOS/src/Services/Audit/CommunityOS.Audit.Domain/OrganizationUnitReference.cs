namespace CommunityOS.Audit.Domain;

/// <summary>
/// Local projection of the organization-unit hierarchy (ADR-016), built from
/// Organization unit events so scope resolution never calls another service at
/// query time. Mirrors the Search projection shape.
/// </summary>
public sealed class OrganizationUnitReference
{
    private OrganizationUnitReference()
    {
    }

    public Guid OrganizationUnitId { get; private set; }
    public Guid? ParentOrganizationUnitId { get; private set; }
    public DateTime LastUpdatedOn { get; private set; }

    public static OrganizationUnitReference Create(Guid id, Guid? parent, DateTime occurredOn) =>
        new()
        {
            OrganizationUnitId = id,
            ParentOrganizationUnitId = parent,
            LastUpdatedOn = occurredOn
        };

    public bool Apply(Guid? parent, DateTime occurredOn)
    {
        if (occurredOn < LastUpdatedOn) return false;
        ParentOrganizationUnitId = parent;
        LastUpdatedOn = occurredOn;
        return true;
    }
}
