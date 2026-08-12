namespace CommunityOS.Organization.Domain.Exceptions;

public sealed class OrganizationNotFoundException(Guid organizationId)
    : Exception($"Organization '{organizationId}' was not found.");

public sealed class OrganizationNameAlreadyExistsException(string name)
    : Exception($"An organization named '{name}' already exists.");

public sealed class OrganizationUnitNotFoundException(Guid organizationUnitId)
    : Exception($"Organization unit '{organizationUnitId}' was not found.");

public sealed class OrganizationUnitNameAlreadyExistsException(string name)
    : Exception($"An organization unit named '{name}' already exists.");

public sealed class OrganizationUnitAlreadyDeactivatedException(Guid organizationUnitId)
    : Exception($"Organization unit '{organizationUnitId}' is already deactivated.");

public sealed class HierarchyCycleException(Guid organizationUnitId)
    : Exception($"Reparenting organization unit '{organizationUnitId}' would create a cycle.");

public sealed class AppointmentNotFoundException(Guid appointmentId)
    : Exception($"Appointment '{appointmentId}' was not found.");

public sealed class AppointmentAlreadyEndedException(Guid appointmentId)
    : Exception($"Appointment '{appointmentId}' has already ended.");

/// <summary>
/// Thrown when a person already holds an overlapping appointment of the same
/// type within the same organization unit. Overlapping appointments are not
/// permitted.
/// </summary>
public sealed class OverlappingAppointmentException(
    Guid personId, Guid organizationUnitId, string appointmentType)
    : Exception(
        $"Person '{personId}' already holds an active '{appointmentType}' appointment " +
        $"in organization unit '{organizationUnitId}' within the requested period.");

public sealed class CommitteeNotFoundException(Guid committeeId)
    : Exception($"Committee '{committeeId}' was not found.");

public sealed class CommitteeMemberNotFoundException(Guid committeeId, Guid personId)
    : Exception($"Person '{personId}' is not a member of committee '{committeeId}'.");

public sealed class InstitutionNotFoundException(Guid institutionId)
    : Exception($"Institution '{institutionId}' was not found.");

public sealed class DelegationFactNotFoundException(Guid delegationFactId)
    : Exception($"Delegation fact '{delegationFactId}' was not found.");

public sealed class DelegationFactAlreadyRevokedException(Guid delegationFactId)
    : Exception($"Delegation fact '{delegationFactId}' has already been revoked.");

public sealed class SelfDelegationFactException()
    : Exception("A subject cannot delegate authority to itself.");

public sealed class InvalidEffectivePeriodException()
    : Exception("EffectiveUntil must be later than EffectiveFrom.");
