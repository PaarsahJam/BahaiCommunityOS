using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Notifications.Domain.Aggregates;

/// <summary>
/// Read-model projection of an organization unit (ADR-016), mirroring Community,
/// Knowledge, Documents, Records and Workflow. Notifications scopes
/// notifications to units, so only the stable unit id is projected — scope
/// checks are resolved live by the Authorization service, never from this read
/// model. Notifications never reads the Organization database.
/// </summary>
public sealed class OrganizationUnitReference : AggregateRoot<Guid>
{
    private OrganizationUnitReference() : base(Guid.Empty)
    {
    }

    private OrganizationUnitReference(Guid id, Guid organizationUnitId, DateTime occurredOn) : base(id)
    {
        OrganizationUnitId = organizationUnitId;
        CreatedOn = occurredOn.ToUniversalTime();
    }

    public Guid OrganizationUnitId { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public static OrganizationUnitReference Create(Guid organizationUnitId, DateTime occurredOn)
    {
        Guard.NotDefault(organizationUnitId, nameof(organizationUnitId));
        return new OrganizationUnitReference(Guid.NewGuid(), organizationUnitId, occurredOn);
    }
}