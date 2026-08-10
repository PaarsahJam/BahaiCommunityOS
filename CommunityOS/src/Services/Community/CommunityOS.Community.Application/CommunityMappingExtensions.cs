using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Entities;
using CommunityAggregate = CommunityOS.Community.Domain.Aggregates.Community;

namespace CommunityOS.Community.Application;

internal static class CommunityMappingExtensions
{
    internal static CommunityDto ToDto(this CommunityAggregate c) =>
        new(c.Id,
            c.Name.Value,
            c.Area.Country,
            c.Area.Region,
            c.Area.City,
            c.Area.Latitude,
            c.Area.Longitude,
            c.Level.Name,
            c.ParentId,
            c.IsActive,
            c.LocalUnits.Select(u => u.ToDto()).ToList().AsReadOnly());

    internal static LocalUnitDto ToDto(this LocalUnit u) =>
        new(u.Id,
            u.Name.Value,
            u.Area.Country,
            u.Area.Region,
            u.Area.City,
            u.ClusterId,
            u.IsActive);

    internal static ClusterDto ToDto(this Cluster cl) =>
        new(cl.Id,
            cl.Name.Value,
            cl.Area.Country,
            cl.Area.Region,
            cl.Area.City,
            cl.RegionId);
}
