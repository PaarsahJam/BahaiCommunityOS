using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Organization.Application.Commands;
using CommunityOS.Organization.Application.DTOs;
using CommunityOS.Organization.Application.Permissions;
using CommunityOS.Organization.Domain.Enumerations;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.Repositories;
using MediatR;

namespace CommunityOS.Organization.Application.Queries;

public sealed record GetAppointmentByIdQuery(Guid ActorId, Guid AppointmentId)
    : IRequest<AppointmentDto>;

internal sealed class GetAppointmentByIdQueryHandler(
    IAppointmentRepository appointments,
    AuthorizationGuard guard) : IRequestHandler<GetAppointmentByIdQuery, AppointmentDto>
{
    public async Task<AppointmentDto> Handle(GetAppointmentByIdQuery request, CancellationToken ct)
    {
        var appointment = await appointments.GetByIdAsync(request.AppointmentId, ct)
            ?? throw new AppointmentNotFoundException(request.AppointmentId);

        var context = new AuthorizationContext(OrganizationUnitId: appointment.OrganizationUnitId);
        await guard.RequireAsync(request.ActorId, OrganizationPermissions.AppointmentRead, context, ct);

        var includeReason = await guard.HasAsync(
            request.ActorId, OrganizationPermissions.AppointmentReadReason, context, ct);

        return AssignAppointmentCommandHandler.Map(appointment, includeReason);
    }
}

public sealed record GetAppointmentsQuery(
    Guid ActorId,
    Guid? PersonId = null,
    Guid? OrganizationUnitId = null,
    DateTime? AsOf = null,
    AppointmentView? View = null) : IRequest<IReadOnlyList<AppointmentDto>>;

internal sealed class GetAppointmentsQueryHandler(
    IAppointmentRepository appointments,
    AuthorizationGuard guard) : IRequestHandler<GetAppointmentsQuery, IReadOnlyList<AppointmentDto>>
{
    public async Task<IReadOnlyList<AppointmentDto>> Handle(GetAppointmentsQuery request, CancellationToken ct)
    {
        var moment = request.AsOf?.ToUniversalTime() ?? DateTime.UtcNow;
        var view = request.View ?? AppointmentView.Current;

        // Person-scoped appointment lookups are cross-cutting and require an
        // explicit grant; unit-scoped lookups use the unit-scoped read grant.
        AuthorizationContext? reasonContext = null;
        if (request.PersonId is { } personId)
        {
            await guard.RequireAsync(request.ActorId, OrganizationPermissions.AppointmentReadPerson, null, ct);
            reasonContext = null;
        }
        else if (request.OrganizationUnitId is { } requestedUnitId)
        {
            reasonContext = new AuthorizationContext(OrganizationUnitId: requestedUnitId);
            await guard.RequireAsync(
                request.ActorId, OrganizationPermissions.AppointmentRead, reasonContext, ct);
        }
        else
        {
            throw new InvalidOperationException("Either PersonId or OrganizationUnitId must be provided.");
        }

        IReadOnlyList<Domain.Aggregates.Appointment> items;
        if (request.PersonId is { } pid)
            items = await appointments.ListAsync(pid, null, moment, view, ct);
        else if (request.OrganizationUnitId is { } unitId)
            items = await appointments.ListAsync(null, unitId, moment, view, ct);
        else
            throw new InvalidOperationException("Either PersonId or OrganizationUnitId must be provided.");

        var includeReason = await guard.HasAsync(
            request.ActorId, OrganizationPermissions.AppointmentReadReason, reasonContext, ct);

        return items
            .Select(a => AssignAppointmentCommandHandler.Map(a, includeReason))
            .ToList();
    }
}
