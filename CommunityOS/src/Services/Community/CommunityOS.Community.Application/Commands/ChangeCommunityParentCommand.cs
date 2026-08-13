using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Community.Application.Permissions;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.Repositories;
using MediatR;

namespace CommunityOS.Community.Application.Commands;

public sealed record ChangeCommunityParentCommand(
    Guid ActorId,
    Guid CommunityId,
    Guid? NewParentId) : IRequest;

internal sealed class ChangeCommunityParentCommandHandler(
    ICommunityRepository communities,
    AuthorizationGuard guard)
    : IRequestHandler<ChangeCommunityParentCommand>
{
    public async Task Handle(ChangeCommunityParentCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, CommunityPermissions.CommunityHierarchyUpdate,
            new AuthorizationContext(ResourceType: "community", ResourceId: cmd.CommunityId), ct);

        var community = await communities.GetByIdAsync(cmd.CommunityId, ct)
            ?? throw new CommunityNotFoundException(cmd.CommunityId);

        community.ChangeParent(cmd.NewParentId);
        await communities.UpdateAsync(community, ct);
    }
}
