using CommunityOS.Organization.Domain.Entities;
using CommunityOS.Organization.Domain.Events;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Organization.Domain.Aggregates;

/// <summary>
/// A node in the organizational hierarchy (e.g. a cluster, a local unit, an
/// institute or a team). Units belong to an organization and keep an
/// effective-dated history of parents so hierarchy changes are auditable and
/// the hierarchy can be reconstructed at any point in time.
/// </summary>
public sealed class OrganizationUnit : AggregateRoot<Guid>
{
    private readonly List<OrganizationUnitParent> _parents = [];

    private OrganizationUnit() : base(Guid.Empty)
    {
        Name = null!;
        UnitType = null!;
    }

    private OrganizationUnit(
        Guid id, Guid organizationId, string name, string unitType) : base(id)
    {
        OrganizationId = organizationId;
        Name = name;
        UnitType = unitType;
        IsActive = true;
        CreatedOn = DateTime.UtcNow;
    }

    public Guid OrganizationId { get; }
    public string Name { get; private set; }
    public string UnitType { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedOn { get; private set; }

    public IReadOnlyList<OrganizationUnitParent> Parents => _parents.AsReadOnly();

    public static OrganizationUnit Create(
        Guid organizationId,
        string name,
        string unitType,
        Guid? parentId,
        EffectivePeriod parentPeriod)
    {
        Guard.NotDefault(organizationId, nameof(organizationId));
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, 200, nameof(name));
        Guard.NotNullOrWhiteSpace(unitType, nameof(unitType));
        Guard.MaxLength(unitType, 100, nameof(unitType));
        Guard.NotNull(parentPeriod, nameof(parentPeriod));

        var unit = new OrganizationUnit(Guid.NewGuid(), organizationId, name.Trim(), unitType.Trim());
        unit._parents.Add(OrganizationUnitParent.Link(parentId, parentPeriod));

        unit.RaiseDomainEvent(new OrganizationUnitCreatedEvent(
            unit.Id, unit.OrganizationId, unit.Name, unit.UnitType, parentId));
        return unit;
    }

    public void UpdateDetails(string name, string unitType)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, 200, nameof(name));
        Guard.NotNullOrWhiteSpace(unitType, nameof(unitType));
        Guard.MaxLength(unitType, 100, nameof(unitType));

        Name = name.Trim();
        UnitType = unitType.Trim();

        RaiseDomainEvent(new OrganizationUnitUpdatedEvent(Id, Name, UnitType));
    }

    /// <summary>
    /// Records an effective-dated parent change. Cycle detection is performed by
    /// the caller (application layer) against the repository because it needs
    /// whole-hierarchy visibility.
    /// </summary>
    public void ChangeParent(Guid? parentId, EffectivePeriod parentPeriod)
    {
        Guard.NotNull(parentPeriod, nameof(parentPeriod));

        if (parentId == Id)
            throw new HierarchyCycleException(Id);

        var current = CurrentParent(DateTime.UtcNow);
        if (current is not null && current.ParentId == parentId)
            throw new InvalidOperationException(
                $"Organization unit '{Id}' is already attached to parent '{parentId}'.");

        _parents.Add(OrganizationUnitParent.Link(parentId, parentPeriod));

        RaiseDomainEvent(new OrganizationUnitParentChangedEvent(
            Id, parentId, parentPeriod.EffectiveFrom, parentPeriod.EffectiveUntil));
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new OrganizationUnitAlreadyDeactivatedException(Id);

        IsActive = false;
    }

    /// <summary>
    /// The parent link effective at <paramref name="moment"/>, or null when the
    /// unit has no parent in effect at that moment (i.e. it is a root unit).
    /// </summary>
    public OrganizationUnitParent? CurrentParent(DateTime moment) =>
        _parents
            .Where(p => p.IsEffectiveAt(moment))
            .OrderByDescending(p => p.Period.EffectiveFrom)
            .FirstOrDefault();

    public Guid? ParentIdAt(DateTime moment) => CurrentParent(moment)?.ParentId;
}
