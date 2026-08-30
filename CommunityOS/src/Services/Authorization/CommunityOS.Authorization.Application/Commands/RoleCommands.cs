using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.DTOs;
using CommunityOS.Authorization.Application.Permissions;
using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Authorization.Domain.Repositories;
using CommunityOS.Authorization.Domain.ValueObjects;
using MediatR;

namespace CommunityOS.Authorization.Application.Commands;

public sealed record CreateRoleCommand(
    Guid ActorId,
    string Code,
    string DisplayName,
    string? Description,
    IReadOnlyList<string> Permissions) : IRequest<RoleDto>;

internal sealed class CreateRoleCommandHandler(
    IRoleRepository roles,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<CreateRoleCommand, RoleDto>
{
    public async Task<RoleDto> Handle(CreateRoleCommand request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, PermissionCatalog.AuthzRoleCreate, null, ct);

        if (await roles.GetByCodeAsync(request.Code, ct) is not null)
            throw new RoleCodeAlreadyExistsException(request.Code);

        var role = Role.Create(request.Code, request.DisplayName, request.Description, request.Permissions);
        await DomainEventPublisher.PublishAsync(role, mediator, ct);
        await roles.AddAsync(role, ct);

        return Map(role);
    }

    internal static RoleDto Map(Role role) => new(
        role.Id,
        role.Code,
        role.DisplayName,
        role.Description,
        role.Enabled,
        role.Permissions);
}

public sealed record UpdateRoleCommand(
    Guid ActorId,
    Guid RoleId,
    string DisplayName,
    string? Description,
    IReadOnlyList<string> Permissions) : IRequest<RoleDto>;

internal sealed class UpdateRoleCommandHandler(
    IRoleRepository roles,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<UpdateRoleCommand, RoleDto>
{
    public async Task<RoleDto> Handle(UpdateRoleCommand request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, PermissionCatalog.AuthzRoleUpdate, null, ct);

        var role = await roles.GetByIdAsync(request.RoleId, ct)
            ?? throw new RoleNotFoundException(request.RoleId);

        role.UpdateDetails(request.DisplayName, request.Description);
        role.UpdatePermissions(request.Permissions);

        await DomainEventPublisher.PublishAsync(role, mediator, ct);
        await roles.UpdateAsync(role, ct);

        return CreateRoleCommandHandler.Map(role);
    }
}

public sealed record AssignRoleCommand(
    Guid ActorId,
    Guid SubjectId,
    Guid RoleId,
    string ScopeType,
    Guid? ScopeId,
    string? ResourceType,
    DateTime? EffectiveFrom,
    DateTime? EffectiveUntil,
    string? Reason) : IRequest<RoleAssignmentDto>;

internal sealed class AssignRoleCommandHandler(
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<AssignRoleCommand, RoleAssignmentDto>
{
    public async Task<RoleAssignmentDto> Handle(AssignRoleCommand request, CancellationToken ct)
    {
        var scope = AuthorizationScopeParser.Parse(request.ScopeType, request.ScopeId, request.ResourceType);

        var role = await roles.GetByIdAsync(request.RoleId, ct)
            ?? throw new RoleNotFoundException(request.RoleId);

        // Assignment targets a scope, so the actor's granting authority is
        // evaluated against that scope (fail closed: no downward cascade
        // without an owned organization hierarchy).
        await guard.RequireAsync(
            request.ActorId,
            PermissionCatalog.AuthzRoleAssign,
            AuthorizationScopeParser.ToContext(scope),
            ct);

        var assignment = RoleAssignment.Create(
            request.SubjectId,
            role.Id,
            role.Code,
            scope,
            request.ActorId,
            DateTime.UtcNow,
            request.EffectiveFrom,
            request.EffectiveUntil,
            request.Reason);

        await DomainEventPublisher.PublishAsync(assignment, mediator, ct);
        await assignments.AddAsync(assignment, ct);

        return Map(assignment);
    }

    internal static RoleAssignmentDto Map(RoleAssignment assignment) => new(
        assignment.Id,
        assignment.SubjectId,
        assignment.RoleId,
        assignment.RoleCode,
        assignment.Scope.Type.Name,
        assignment.Scope.ScopeId,
        assignment.Scope.ResourceType,
        assignment.GrantedBy,
        assignment.GrantedAt,
        assignment.EffectiveFrom,
        assignment.EffectiveUntil,
        assignment.Reason,
        assignment.IsRevoked,
        assignment.RevokedBy,
        assignment.RevokedAt);
}

public sealed record RevokeRoleCommand(
    Guid ActorId,
    Guid AssignmentId,
    string? Reason) : IRequest<RoleAssignmentDto>;

internal sealed class RevokeRoleCommandHandler(
    IRoleAssignmentRepository assignments,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<RevokeRoleCommand, RoleAssignmentDto>
{
    public async Task<RoleAssignmentDto> Handle(RevokeRoleCommand request, CancellationToken ct)
    {
        var assignment = await assignments.GetByIdAsync(request.AssignmentId, ct)
            ?? throw new RoleAssignmentNotFoundException(request.AssignmentId);

        await guard.RequireAsync(
            request.ActorId,
            PermissionCatalog.AuthzRoleRevoke,
            AuthorizationScopeParser.ToContext(assignment.Scope),
            ct);

        assignment.Revoke(request.ActorId, request.Reason);

        await DomainEventPublisher.PublishAsync(assignment, mediator, ct);
        await assignments.UpdateAsync(assignment, ct);

        return AssignRoleCommandHandler.Map(assignment);
    }
}
