using CommunityOS.Organization.Domain.Events;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Organization.Domain.Aggregates;

/// <summary>
/// A Faith institution that operates within the community (e.g. a National
/// Spiritual Assembly, a Regional Council or a Local Spiritual Assembly).
/// Organizations own the organization units that form the hierarchy and are
/// the root for appointments, committees and delegation facts.
/// </summary>
public sealed class Organization : AggregateRoot<Guid>
{
    private Organization() : base(Guid.Empty)
    {
        Name = null!;
        OrganizationType = null!;
        Jurisdiction = null!;
        Status = null!;
    }

    private Organization(Guid id, string name, string organizationType, Jurisdiction jurisdiction)
        : base(id)
    {
        Name = name;
        OrganizationType = organizationType;
        Jurisdiction = jurisdiction;
        Status = "active";
        CreatedOn = DateTime.UtcNow;
    }

    public string Name { get; private set; }
    public string OrganizationType { get; private set; }
    public Jurisdiction Jurisdiction { get; private set; }
    public string Status { get; private set; }
    public DateTime? EstablishedOn { get; private set; }
    public DateTime? DissolvedOn { get; private set; }
    public DateTime CreatedOn { get; private set; }

    public static Organization Create(
        string name,
        string organizationType,
        Jurisdiction jurisdiction,
        DateTime? establishedOn = null)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, 200, nameof(name));
        Guard.NotNullOrWhiteSpace(organizationType, nameof(organizationType));
        Guard.MaxLength(organizationType, 100, nameof(organizationType));
        Guard.NotNull(jurisdiction, nameof(jurisdiction));

        var organization = new Organization(
            Guid.NewGuid(),
            name.Trim(),
            organizationType.Trim(),
            jurisdiction);

        organization.EstablishedOn = establishedOn?.ToUniversalTime();

        organization.RaiseDomainEvent(new OrganizationCreatedEvent(
            organization.Id,
            organization.Name,
            organization.OrganizationType,
            organization.Jurisdiction.Type.Name,
            organization.Jurisdiction.ScopeId));
        return organization;
    }

    public void UpdateDetails(string name, Jurisdiction jurisdiction)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, 200, nameof(name));
        Guard.NotNull(jurisdiction, nameof(jurisdiction));

        Name = name.Trim();
        Jurisdiction = jurisdiction;

        RaiseDomainEvent(new OrganizationUpdatedEvent(
            Id, Name, OrganizationType, Jurisdiction.Type.Name, Jurisdiction.ScopeId));
    }

    public void Dissolve(DateTime dissolvedOn)
    {
        if (Status == "dissolved")
            return;

        Status = "dissolved";
        DissolvedOn = dissolvedOn.ToUniversalTime();

        RaiseDomainEvent(new OrganizationUpdatedEvent(
            Id, Name, OrganizationType, Jurisdiction.Type.Name, Jurisdiction.ScopeId));
    }

    public void Suspend()
    {
        if (Status == "suspended")
            return;

        Status = "suspended";
        RaiseDomainEvent(new OrganizationUpdatedEvent(
            Id, Name, OrganizationType, Jurisdiction.Type.Name, Jurisdiction.ScopeId));
    }

    public void Activate()
    {
        Status = "active";
        RaiseDomainEvent(new OrganizationUpdatedEvent(
            Id, Name, OrganizationType, Jurisdiction.Type.Name, Jurisdiction.ScopeId));
    }
}
