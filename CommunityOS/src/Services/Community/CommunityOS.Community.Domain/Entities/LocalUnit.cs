using CommunityOS.Community.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.Entities;

public sealed class LocalUnit : Entity<Guid>
{
    public CommunityName Name { get; private set; }
    public GeographicArea Area { get; private set; }
    public Guid ClusterId { get; private set; }
    public bool IsActive { get; private set; }

    private LocalUnit(Guid id, CommunityName name, GeographicArea area, Guid clusterId) : base(id)
    {
        Name = name;
        Area = area;
        ClusterId = clusterId;
        IsActive = true;
    }

    public static LocalUnit Create(CommunityName name, GeographicArea area, Guid clusterId)
    {
        Guard.NotNull(name, nameof(name));
        Guard.NotNull(area, nameof(area));
        Guard.NotDefault(clusterId, nameof(clusterId));
        return new LocalUnit(Guid.NewGuid(), name, area, clusterId);
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
