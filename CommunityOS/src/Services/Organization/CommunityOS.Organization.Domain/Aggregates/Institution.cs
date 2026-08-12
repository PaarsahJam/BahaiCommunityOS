using CommunityOS.Organization.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Organization.Domain.Aggregates;

/// <summary>
/// A canonical record of a Faith institution (e.g. the Universal House of
/// Justice, an Auxiliary Board or a Continental Board of Counsellors).
/// Institutions are the bodies the Faith formally recognizes; organizations
/// are the operational administrative bodies that carry units, committees and
/// appointments.
/// </summary>
public sealed class Institution : AggregateRoot<Guid>
{
    private Institution() : base(Guid.Empty)
    {
        Name = null!;
        InstitutionType = null!;
        Jurisdiction = null!;
    }

    private Institution(
        Guid id, string name, string institutionType, Jurisdiction jurisdiction)
        : base(id)
    {
        Name = name;
        InstitutionType = institutionType;
        Jurisdiction = jurisdiction;
        IsActive = true;
        CreatedOn = DateTime.UtcNow;
    }

    public string Name { get; private set; }
    public string InstitutionType { get; private set; }
    public Jurisdiction Jurisdiction { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? EstablishedOn { get; private set; }
    public DateTime CreatedOn { get; private set; }

    public static Institution Create(
        string name,
        string institutionType,
        Jurisdiction jurisdiction,
        DateTime? establishedOn = null)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, 200, nameof(name));
        Guard.NotNullOrWhiteSpace(institutionType, nameof(institutionType));
        Guard.MaxLength(institutionType, 100, nameof(institutionType));
        Guard.NotNull(jurisdiction, nameof(jurisdiction));

        var institution = new Institution(
            Guid.NewGuid(), name.Trim(), institutionType.Trim(), jurisdiction);

        institution.EstablishedOn = establishedOn?.ToUniversalTime();
        return institution;
    }

    public void UpdateDetails(string name, Jurisdiction jurisdiction)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, 200, nameof(name));
        Guard.NotNull(jurisdiction, nameof(jurisdiction));

        Name = name.Trim();
        Jurisdiction = jurisdiction;
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
