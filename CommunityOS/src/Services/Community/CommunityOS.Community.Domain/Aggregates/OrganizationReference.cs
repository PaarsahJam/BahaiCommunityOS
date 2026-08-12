using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.Aggregates;

/// <summary>
/// A Community-side read-model of an organization fact owned by the
/// Organization service. Community does not own organizational hierarchy; this
/// projection is kept in sync by consuming Organization integration events so
/// Community can reference organization facts (id, name, type, jurisdiction)
/// without owning them. See ADR-016.
/// </summary>
public sealed class OrganizationReference : AggregateRoot<Guid>
{
    private OrganizationReference() : base(Guid.Empty)
    {
        Name = null!;
        OrganizationType = null!;
        JurisdictionType = null!;
    }

    private OrganizationReference(
        Guid organizationId,
        string name,
        string organizationType,
        string jurisdictionType,
        Guid? jurisdictionScopeId,
        DateTime occurredOn) : base(organizationId)
    {
        Name = name;
        OrganizationType = organizationType;
        JurisdictionType = jurisdictionType;
        JurisdictionScopeId = jurisdictionScopeId;
        LastSeenOn = occurredOn;
    }

    public string Name { get; private set; }
    public string OrganizationType { get; private set; }
    public string JurisdictionType { get; private set; }
    public Guid? JurisdictionScopeId { get; private set; }
    public DateTime LastSeenOn { get; private set; }

    public static OrganizationReference Create(
        Guid organizationId,
        string name,
        string organizationType,
        string jurisdictionType,
        Guid? jurisdictionScopeId,
        DateTime occurredOn)
    {
        Guard.NotDefault(organizationId, nameof(organizationId));
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, 200, nameof(name));
        Guard.NotNullOrWhiteSpace(organizationType, nameof(organizationType));
        Guard.MaxLength(organizationType, 100, nameof(organizationType));
        Guard.NotNullOrWhiteSpace(jurisdictionType, nameof(jurisdictionType));

        return new OrganizationReference(
            organizationId,
            name.Trim(),
            organizationType.Trim(),
            jurisdictionType.Trim(),
            jurisdictionScopeId,
            occurredOn.ToUniversalTime());
    }

    public void Sync(
        string name,
        string organizationType,
        string jurisdictionType,
        Guid? jurisdictionScopeId,
        DateTime occurredOn)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, 200, nameof(name));
        Guard.NotNullOrWhiteSpace(organizationType, nameof(organizationType));
        Guard.MaxLength(organizationType, 100, nameof(organizationType));
        Guard.NotNullOrWhiteSpace(jurisdictionType, nameof(jurisdictionType));

        Name = name.Trim();
        OrganizationType = organizationType.Trim();
        JurisdictionType = jurisdictionType.Trim();
        JurisdictionScopeId = jurisdictionScopeId;
        LastSeenOn = occurredOn.ToUniversalTime();
    }
}
