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

public sealed record RecordParticipationCommand(
    Guid ActorId,
    Guid PersonId,
    string TargetType,
    Guid TargetId,
    string? Role,
    string Status,
    DateTime EffectiveFrom) : IRequest<ParticipationDto>;

internal sealed class RecordParticipationCommandHandler(
    IParticipationRepository participations,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<RecordParticipationCommand, ParticipationDto>
{
    public async Task<ParticipationDto> Handle(RecordParticipationCommand request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, CommunityPermissions.ParticipationCreate,
            new AuthorizationContext(ResourceType: "participation"), ct);

        var targetType = ParticipationTargetType.FromName(request.TargetType);
        if (await participations.ExistsAsync(request.PersonId, targetType, request.TargetId, ct))
            throw new DuplicateParticipationException(request.PersonId, targetType.Name, request.TargetId);

        var participation = Participation.Create(
            request.PersonId,
            targetType,
            request.TargetId,
            request.Role,
            ParticipationStatus.FromName(request.Status),
            EffectivePeriod.Create(request.EffectiveFrom));

        await participations.AddAsync(participation, ct);
        await DomainEvents.PublishAsync(participation, mediator, ct);

        return participation.ToDto();
    }
}

public sealed record UpdateParticipationStatusCommand(
    Guid ActorId,
    Guid ParticipationId,
    string Status) : IRequest<ParticipationDto>;

internal sealed class UpdateParticipationStatusCommandHandler(
    IParticipationRepository participations,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<UpdateParticipationStatusCommand, ParticipationDto>
{
    public async Task<ParticipationDto> Handle(UpdateParticipationStatusCommand request, CancellationToken ct)
    {
        var participation = await participations.GetByIdAsync(request.ParticipationId, ct)
            ?? throw new ParticipationNotFoundException(request.ParticipationId);

        await guard.RequireAsync(request.ActorId, CommunityPermissions.ParticipationUpdate,
            new AuthorizationContext(ResourceType: "participation", ResourceId: request.ParticipationId), ct);

        participation.UpdateStatus(ParticipationStatus.FromName(request.Status));

        await participations.UpdateAsync(participation, ct);
        await DomainEvents.PublishAsync(participation, mediator, ct);

        return participation.ToDto();
    }
}
