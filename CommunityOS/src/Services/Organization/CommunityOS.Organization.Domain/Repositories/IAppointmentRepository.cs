using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Enumerations;
using CommunityOS.Organization.Domain.ValueObjects;

namespace CommunityOS.Organization.Domain.Repositories;

public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> ListByPersonAsync(
        Guid personId, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> ListByOrganizationUnitAsync(
        Guid organizationUnitId, CancellationToken ct = default);

    /// <summary>
    /// Lists appointments filtered by person and/or organization unit from a
    /// temporal <paramref name="view"/> anchored at <paramref name="asOf"/>.
    /// Both filters are optional but at least one must be supplied by callers.
    /// </summary>
    Task<IReadOnlyList<Appointment>> ListAsync(
        Guid? personId,
        Guid? organizationUnitId,
        DateTime? asOf,
        AppointmentView view,
        CancellationToken ct = default);

    /// <summary>
    /// True when the person already holds an active appointment of the same
    /// type within the unit whose effective window overlaps the requested
    /// period. Enforces the non-overlapping appointment rule.
    /// </summary>
    Task<bool> HasOverlappingAsync(
        Guid personId,
        Guid organizationUnitId,
        string appointmentType,
        EffectivePeriod period,
        CancellationToken ct = default);

    Task AddAsync(Appointment appointment, CancellationToken ct = default);
    Task UpdateAsync(Appointment appointment, CancellationToken ct = default);
}
