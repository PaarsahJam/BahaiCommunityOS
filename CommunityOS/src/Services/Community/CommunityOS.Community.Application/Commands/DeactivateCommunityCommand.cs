using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.Repositories;
using MediatR;

namespace CommunityOS.Community.Application.Commands;

public sealed record DeactivateCommunityCommand(Guid CommunityId) : IRequest;

internal sealed class DeactivateCommunityCommandHandler(ICommunityRepository communities)
    : IRequestHandler<DeactivateCommunityCommand>
{
    public async Task Handle(DeactivateCommunityCommand cmd, CancellationToken ct)
    {
        var community = await communities.GetByIdAsync(cmd.CommunityId, ct)
            ?? throw new CommunityNotFoundException(cmd.CommunityId);

        community.Deactivate();
        await communities.UpdateAsync(community, ct);
    }
}
