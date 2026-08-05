using CommunityOS.Community.Domain.Entities;
using CommunityOS.Community.Domain.Events;
using CommunityOS.Community.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.Aggregates;

public sealed class Community : AggregateRoot<Guid>
{
    private readonly List<LocalUnit> _localUnits = [];

    public CommunityName Name { get; private set; }
    public GeographicArea Area { get; private set; }
    public HierarchyLevel Level { get; private set; }
    public Guid? ParentId { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyList<LocalUnit> LocalUnits => _localUnits.AsReadOnly();

    private Community(Guid id, CommunityName name, GeographicArea area,
        HierarchyLevel level, Guid? parentId) : base(id)
    {
        Name = name;
        Area = area;
        Level = level;
        ParentId = parentId;
        IsActive = true;
    }

    public static Community Create(CommunityName name, GeographicArea area,
        HierarchyLevel level, Guid? parentId = null)
    {
        Guard.NotNull(name, nameof(name));
        Guard.NotNull(area, nameof(area));
        Guard.NotNull(level, nameof(level));
        var community = new Community(Guid.NewGuid(), name, area, level, parentId);
        community.RaiseDomainEvent(new CommunityCreatedEvent(community.Id, name.Value));
        return community;
    }

    public void AddLocalUnit(LocalUnit localUnit)
    {
        Guard.NotNull(localUnit, nameof(localUnit));
        if (_localUnits.Any(u => u.Id == localUnit.Id)) return;
        _localUnits.Add(localUnit);
        RaiseDomainEvent(new LocalUnitAddedEvent(Id, localUnit.Id, localUnit.Name.Value));
    }

    public void ChangeParent(Guid? newParentId)
    {
        ParentId = newParentId;
        RaiseDomainEvent(new CommunityHierarchyChangedEvent(Id, newParentId));
    }

    public void UpdateDetails(CommunityName name, GeographicArea area)
    {
        Guard.NotNull(name, nameof(name));
        Guard.NotNull(area, nameof(area));
        Name = name;
        Area = area;
    }

    public void Deactivate() => IsActive = false;
}
