using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.Repositories;
using MediatR;

namespace CommunityOS.Community.Application.Commands;

public sealed record ChangeCommunityParentCommand(Guid CommunityId, Guid? NewParentId) : IRequest;

internal sealed class ChangeCommunityParentCommandHandler(ICommunityRepository communities)
    : IRequestHandler<ChangeCommunityParentCommand>
{
    public async Task Handle(ChangeCommunityParentCommand cmd, CancellationToken ct)
    {
        var community = await communities.GetByIdAsync(cmd.CommunityId, ct)
            ?? throw new CommunityNotFoundException(cmd.CommunityId);

        community.ChangeParent(cmd.NewParentId);
        await communities.UpdateAsync(community, ct);
    }
}
