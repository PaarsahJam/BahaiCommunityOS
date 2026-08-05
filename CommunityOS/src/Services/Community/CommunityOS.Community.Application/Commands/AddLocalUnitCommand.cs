using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Domain.Entities;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Domain.ValueObjects;
using MediatR;

namespace CommunityOS.Community.Application.Commands;

public sealed record AddLocalUnitCommand(
    Guid CommunityId,
    string Name,
    string Country,
    string? Region,
    string? City,
    Guid ClusterId) : IRequest<LocalUnitDto>;

internal sealed class AddLocalUnitCommandHandler(ICommunityRepository communities)
    : IRequestHandler<AddLocalUnitCommand, LocalUnitDto>
{
    public async Task<LocalUnitDto> Handle(AddLocalUnitCommand cmd, CancellationToken ct)
    {
        var community = await communities.GetByIdAsync(cmd.CommunityId, ct)
            ?? throw new CommunityNotFoundException(cmd.CommunityId);

        var name = CommunityName.Create(cmd.Name);
        var area = GeographicArea.Create(cmd.Country, cmd.Region, cmd.City);
        var unit = LocalUnit.Create(name, area, cmd.ClusterId);

        community.AddLocalUnit(unit);
        await communities.UpdateAsync(community, ct);
        return unit.ToDto();
    }
}
