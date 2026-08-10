using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Domain.ValueObjects;
using CommunityAggregate = CommunityOS.Community.Domain.Aggregates.Community;
using MediatR;

namespace CommunityOS.Community.Application.Commands;

public sealed record CreateCommunityCommand(
    string Name,
    string Country,
    string? Region,
    string? City,
    double? Latitude,
    double? Longitude,
    int HierarchyLevelId,
    Guid? ParentId) : IRequest<CommunityDto>;

internal sealed class CreateCommunityCommandHandler(ICommunityRepository communities)
    : IRequestHandler<CreateCommunityCommand, CommunityDto>
{
    public async Task<CommunityDto> Handle(CreateCommunityCommand cmd, CancellationToken ct)
    {
        var name  = CommunityName.Create(cmd.Name);
        var area  = GeographicArea.Create(cmd.Country, cmd.Region, cmd.City, cmd.Latitude, cmd.Longitude);
        var level = HierarchyLevel.FromId(cmd.HierarchyLevelId);

        var community = CommunityAggregate.Create(name, area, level, cmd.ParentId);

        await communities.AddAsync(community, ct);
        return community.ToDto();
    }
}
