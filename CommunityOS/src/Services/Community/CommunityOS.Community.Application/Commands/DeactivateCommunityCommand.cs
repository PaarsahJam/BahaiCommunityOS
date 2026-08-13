using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Community.Application.Permissions;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.Repositories;
using MediatR;

namespace CommunityOS.Community.Application.Commands;

public sealed record DeactivateCommunityCommand(Guid ActorId, Guid CommunityId) : IRequest;

internal sealed class DeactivateCommunityCommandHandler(
    ICommunityRepository communities,
    AuthorizationGuard guard)
    : IRequestHandler<DeactivateCommunityCommand>
{
    public async Task Handle(DeactivateCommunityCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, CommunityPermissions.CommunityHierarchyDeactivate,
            new AuthorizationContext(ResourceType: "community", ResourceId: cmd.CommunityId), ct);

        var community = await communities.GetByIdAsync(cmd.CommunityId, ct)
            ?? throw new CommunityNotFoundException(cmd.CommunityId);

        community.Deactivate();
        await communities.UpdateAsync(community, ct);
    }
}
