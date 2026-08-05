using CommunityOS.Community.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.Entities;

public sealed class Cluster : Entity<Guid>
{
    public CommunityName Name { get; private set; }
    public GeographicArea Area { get; private set; }
    public Guid RegionId { get; private set; }

    private Cluster(Guid id, CommunityName name, GeographicArea area, Guid regionId) : base(id)
    {
        Name = name;
        Area = area;
        RegionId = regionId;
    }

    public static Cluster Create(CommunityName name, GeographicArea area, Guid regionId)
    {
        Guard.NotNull(name, nameof(name));
        Guard.NotNull(area, nameof(area));
        Guard.NotDefault(regionId, nameof(regionId));
        return new Cluster(Guid.NewGuid(), name, area, regionId);
    }
}
