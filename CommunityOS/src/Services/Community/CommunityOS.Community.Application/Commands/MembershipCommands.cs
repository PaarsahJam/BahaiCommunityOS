using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Community.Application.DTOs;
using CommunityOS.Community.Application.Permissions;
using CommunityOS.Community.Application.Pipeline;
using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Domain.ValueObjects;
using MediatR;
using DomainEvents = CommunityOS.Community.Application.Pipeline.DomainEventPublisher;

namespace CommunityOS.Community.Application.Commands;

public sealed record CreateMembershipCommand(
    Guid ActorId,
    Guid PersonId,
    string Status,
    DateTime EffectiveFrom) : IRequest<MembershipDto>;

internal sealed class CreateMembershipCommandHandler(
    IMembershipRepository memberships,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<CreateMembershipCommand, MembershipDto>
{
    public async Task<MembershipDto> Handle(CreateMembershipCommand request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, CommunityPermissions.MembershipCreate,
            new AuthorizationContext(ResourceType: "person", ResourceId: request.PersonId), ct);

        if (await memberships.GetByPersonAsync(request.PersonId, ct) is not null)
            throw new DuplicateMembershipException(request.PersonId);

        var membership = Membership.Create(
            request.PersonId,
            MembershipStatus.FromName(request.Status),
            EffectivePeriod.Create(request.EffectiveFrom));

        await memberships.AddAsync(membership, ct);
        await DomainEvents.PublishAsync(membership, mediator, ct);

        return membership.ToDto();
    }
}

public sealed record ChangeMembershipStatusCommand(
    Guid ActorId,
    Guid MembershipId,
    string Status,
    DateTime? EffectiveFrom) : IRequest<MembershipDto>;

internal sealed class ChangeMembershipStatusCommandHandler(
    IMembershipRepository memberships,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<ChangeMembershipStatusCommand, MembershipDto>
{
    public async Task<MembershipDto> Handle(ChangeMembershipStatusCommand request, CancellationToken ct)
    {
        var membership = await memberships.GetByIdAsync(request.MembershipId, ct)
            ?? throw new MembershipNotFoundException(request.MembershipId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.MembershipUpdate,
            new AuthorizationContext(ResourceType: "membership", ResourceId: request.MembershipId), ct);

        membership.ChangeStatus(MembershipStatus.FromName(request.Status), request.EffectiveFrom);

        await memberships.UpdateAsync(membership, ct);
        await DomainEvents.PublishAsync(membership, mediator, ct);

        return membership.ToDto();
    }
}
