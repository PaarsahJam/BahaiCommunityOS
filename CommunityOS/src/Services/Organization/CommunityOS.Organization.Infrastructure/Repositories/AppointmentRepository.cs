using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Enumerations;
using CommunityOS.Organization.Domain.Repositories;
using CommunityOS.Organization.Domain.ValueObjects;
using CommunityOS.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Organization.Infrastructure.Repositories;

public sealed class AppointmentRepository(OrganizationDbContext db) : IAppointmentRepository
{
    public async Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Appointments.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<Appointment>> ListByPersonAsync(
        Guid personId, CancellationToken ct = default) =>
        await db.Appointments
            .AsNoTracking()
            .Where(x => x.PersonId == personId)
            .OrderBy(x => x.Period.EffectiveFrom)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Appointment>> ListByOrganizationUnitAsync(
        Guid organizationUnitId, CancellationToken ct = default) =>
        await db.Appointments
            .AsNoTracking()
            .Where(x => x.OrganizationUnitId == organizationUnitId)
            .OrderBy(x => x.Period.EffectiveFrom)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Appointment>> ListAsync(
        Guid? personId,
        Guid? organizationUnitId,
        DateTime? asOf,
        AppointmentView view,
        CancellationToken ct = default)
    {
        var moment = asOf?.ToUniversalTime() ?? DateTime.UtcNow;
        var query = db.Appointments.AsNoTracking();

        if (personId is { } pid)
            query = query.Where(a => a.PersonId == pid);
        if (organizationUnitId is { } unitId)
            query = query.Where(a => a.OrganizationUnitId == unitId);

        query = view switch
        {
            AppointmentView.Current => query.Where(a =>
                a.Status != AppointmentStatus.Ended &&
                a.Period.EffectiveFrom <= moment &&
                (a.Period.EffectiveUntil == null || a.Period.EffectiveUntil > moment)),
            AppointmentView.Historical => query.Where(a =>
                a.Period.EffectiveUntil != null && a.Period.EffectiveUntil <= moment),
            AppointmentView.Upcoming => query.Where(a =>
                a.Status != AppointmentStatus.Ended && a.Period.EffectiveFrom > moment),
            AppointmentView.Ended => query.Where(a => a.Status == AppointmentStatus.Ended),
            _ => query
        };

        return await query.OrderBy(a => a.Period.EffectiveFrom).ToListAsync(ct);
    }

    public async Task<bool> HasOverlappingAsync(
        Guid personId,
        Guid organizationUnitId,
        string appointmentType,
        EffectivePeriod period,
        CancellationToken ct = default)
    {
        var from = period.EffectiveFrom;
        var until = period.EffectiveUntil;

        return await db.Appointments.AnyAsync(a =>
            a.PersonId == personId &&
            a.OrganizationUnitId == organizationUnitId &&
            a.AppointmentType == appointmentType &&
            a.Status != AppointmentStatus.Ended &&
            a.Period.EffectiveFrom < (until ?? DateTime.MaxValue) &&
            (a.Period.EffectiveUntil ?? DateTime.MaxValue) > from, ct);
    }

    public async Task AddAsync(Appointment appointment, CancellationToken ct = default)
    {
        await db.Appointments.AddAsync(appointment, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Appointment appointment, CancellationToken ct = default)
    {
        db.Appointments.Update(appointment);
        await db.SaveChangesAsync(ct);
    }
}