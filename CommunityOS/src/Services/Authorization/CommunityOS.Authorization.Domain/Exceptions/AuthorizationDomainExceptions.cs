namespace CommunityOS.Authorization.Domain.Exceptions;

public sealed class RoleNotFoundException(Guid roleId)
    : Exception($"Role '{roleId}' was not found.");

public sealed class RoleCodeAlreadyExistsException(string code)
    : Exception($"A role with code '{code}' already exists.");

public sealed class RoleAssignmentNotFoundException(Guid assignmentId)
    : Exception($"Role assignment '{assignmentId}' was not found.");

public sealed class RoleAssignmentAlreadyRevokedException(Guid assignmentId)
    : Exception($"Role assignment '{assignmentId}' has already been revoked.");

public sealed class InvalidEffectiveRangeException()
    : Exception("EffectiveUntil must not precede EffectiveFrom.");

public sealed class InvalidPermissionException(string permission)
    : Exception($"'{permission}' is not a valid permission name.");

public sealed class InvalidRelationException(string relation)
    : Exception($"'{relation}' is not a valid relationship name.");

public sealed class RelationshipNotFoundException(Guid relationshipId)
    : Exception($"Relationship '{relationshipId}' was not found.");

public sealed class DelegationNotFoundException(Guid delegationId)
    : Exception($"Delegation '{delegationId}' was not found.");

public sealed class DelegationAlreadyRevokedException(Guid delegationId)
    : Exception($"Delegation '{delegationId}' has already been revoked.");

public sealed class SelfDelegationException()
    : Exception("A subject cannot delegate authority to itself.");

public sealed class InvalidDelegationPeriodException()
    : Exception("Delegation expiration must occur after its start.");

public sealed class InvalidDelegationScopeException()
    : Exception("Delegation must be scoped to a specific organization unit, committee, or resource.");

public sealed class InvalidDelegationRequestException(string message)
    : Exception(message);

public sealed class BreakGlassRequestNotFoundException(Guid requestId)
    : Exception($"Break-glass request '{requestId}' was not found.");

public sealed class SelfApprovalForbiddenException()
    : Exception("A subject cannot approve its own break-glass request.");

public sealed class BreakGlassAlreadyProcessedException(Guid requestId)
    : Exception($"Break-glass request '{requestId}' has already been processed.");

public sealed class BreakGlassGlobalScopeForbiddenException()
    : Exception("Break-glass access must be narrowly scoped and cannot be global.");

public sealed class InvalidBreakGlassRequestException(string message)
    : Exception(message);

/// <summary>
/// Thrown when the acting subject is not authorized to perform a protected
/// authorization-administration operation. Defaults to fail-closed.
/// </summary>
public sealed class AuthorizationForbiddenException(string permission)
    : Exception($"The operation requires permission '{permission}'.");
