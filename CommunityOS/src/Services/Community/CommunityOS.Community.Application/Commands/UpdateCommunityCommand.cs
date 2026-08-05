using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Domain.ValueObjects;
using MediatR;

namespace CommunityOS.Community.Application.Commands;

public sealed record UpdateCommunityCommand(
    Guid CommunityId,
    string Name,
    string Country,
    string? Region,
    string? City,
    double? Latitude,
    double? Longitude) : IRequest;

internal sealed class UpdateCommunityCommandHandler(ICommunityRepository communities)
    : IRequestHandler<UpdateCommunityCommand>
{
    public async Task Handle(UpdateCommunityCommand cmd, CancellationToken ct)
    {
        var community = await communities.GetByIdAsync(cmd.CommunityId, ct)
            ?? throw new CommunityNotFoundException(cmd.CommunityId);

        var name = CommunityName.Create(cmd.Name);
        var area = GeographicArea.Create(cmd.Country, cmd.Region, cmd.City, cmd.Latitude, cmd.Longitude);

        community.UpdateDetails(name, area);
        await communities.UpdateAsync(community, ct);
    }
}
