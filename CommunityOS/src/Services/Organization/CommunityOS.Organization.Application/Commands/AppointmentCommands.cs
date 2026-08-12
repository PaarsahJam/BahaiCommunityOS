using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Organization.Application.DTOs;
using CommunityOS.Organization.Application.Permissions;
using CommunityOS.Organization.Application.Pipeline;
using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.Repositories;
using CommunityOS.Organization.Domain.ValueObjects;
using MediatR;
using DomainEvents = CommunityOS.Organization.Application.Pipeline.DomainEventPublisher;

namespace CommunityOS.Organization.Application.Commands;

public sealed record AssignAppointmentCommand(
    Guid ActorId,
    Guid PersonId,
    Guid OrganizationUnitId,
    string AppointmentType,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil,
    string? Reason) : IRequest<AppointmentDto>;

internal sealed class AssignAppointmentCommandHandler(
    IAppointmentRepository appointments,
    IOrganizationUnitRepository units,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<AssignAppointmentCommand, AppointmentDto>
{
    public async Task<AppointmentDto> Handle(AssignAppointmentCommand request, CancellationToken ct)
    {
        if (await units.GetByIdAsync(request.OrganizationUnitId, ct) is null)
            throw new OrganizationUnitNotFoundException(request.OrganizationUnitId);

        await guard.RequireAsync(
            request.ActorId,
            OrganizationPermissions.AppointmentAssign,
            new AuthorizationContext(OrganizationUnitId: request.OrganizationUnitId),
            ct);

        var period = EffectivePeriod.Create(request.EffectiveFrom, request.EffectiveUntil);
        if (await appointments.HasOverlappingAsync(
                request.PersonId, request.OrganizationUnitId, request.AppointmentType, period, ct))
            throw new OverlappingAppointmentException(
                request.PersonId, request.OrganizationUnitId, request.AppointmentType);

        var appointment = Appointment.Create(
            request.PersonId,
            request.OrganizationUnitId,
            request.AppointmentType,
            period,
            request.ActorId,
            request.Reason);

        await appointments.AddAsync(appointment, ct);
        await DomainEvents.PublishAsync(appointment, mediator, ct);

        return Map(appointment);
    }

    internal static AppointmentDto Map(Appointment appointment, bool includeReason = true) => new(
        appointment.Id,
        appointment.PersonId,
        appointment.OrganizationUnitId,
        appointment.AppointmentType,
        appointment.Status.Name,
        appointment.Period.EffectiveFrom,
        appointment.Period.EffectiveUntil,
        appointment.AssignedBy,
        appointment.AssignedOn,
        appointment.EndedBy,
        appointment.EndedOn,
        includeReason ? appointment.Reason : null);
}

public sealed record EndAppointmentCommand(
    Guid ActorId,
    Guid AppointmentId,
    string? Reason) : IRequest<AppointmentDto>;

internal sealed class EndAppointmentCommandHandler(
    IAppointmentRepository appointments,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<EndAppointmentCommand, AppointmentDto>
{
    public async Task<AppointmentDto> Handle(EndAppointmentCommand request, CancellationToken ct)
    {
        var appointment = await appointments.GetByIdAsync(request.AppointmentId, ct)
            ?? throw new AppointmentNotFoundException(request.AppointmentId);

        await guard.RequireAsync(
            request.ActorId,
            OrganizationPermissions.AppointmentEnd,
            new AuthorizationContext(OrganizationUnitId: appointment.OrganizationUnitId),
            ct);

        appointment.End(request.ActorId, request.Reason);

        await appointments.UpdateAsync(appointment, ct);
        await DomainEvents.PublishAsync(appointment, mediator, ct);

        return AssignAppointmentCommandHandler.Map(appointment);
    }
}
