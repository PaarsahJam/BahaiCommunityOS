namespace CommunityOS.Correspondence.Domain;

/// <summary>
/// Projection-only read model of organization units (ADR-016 scoping
/// infrastructure), maintained from Organization unit events. Scope checks
/// need only the stable unit id and hierarchy; authorization is always
/// resolved live by the Authorization service, never from this projection.
/// </summary>
public sealed class OrganizationUnitReference
{
    private OrganizationUnitReference()
    {
    }

    public Guid OrganizationUnitId { get; private set; }

    public Guid? ParentOrganizationUnitId { get; private set; }

    public DateTime? LastUpdatedOn { get; private set; }

    public static OrganizationUnitReference Create(
        Guid organizationUnitId, Guid? parentOrganizationUnitId, DateTime occurredOn) =>
        new()
        {
            OrganizationUnitId = organizationUnitId,
            ParentOrganizationUnitId = parentOrganizationUnitId,
            LastUpdatedOn = occurredOn
        };

    /// <summary>Applies a newer fact; returns false for stale replays.</summary>
    public bool Apply(Guid? parentOrganizationUnitId, DateTime occurredOn)
    {
        if (LastUpdatedOn is { } last && occurredOn <= last)
        {
            return false;
        }

        ParentOrganizationUnitId = parentOrganizationUnitId;
        LastUpdatedOn = occurredOn;
        return true;
    }
}
