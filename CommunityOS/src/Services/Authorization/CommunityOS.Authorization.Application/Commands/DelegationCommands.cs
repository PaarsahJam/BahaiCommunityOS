using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.DTOs;
using CommunityOS.Authorization.Application.Permissions;
using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Authorization.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Options;

namespace CommunityOS.Authorization.Application.Commands;

public sealed record GrantDelegationCommand(
    Guid ActorId,
    Guid DelegateId,
    IReadOnlyList<string> Permissions,
    string ScopeType,
    Guid? ScopeId,
    string? ResourceType,
    DateTime? StartsOn,
    DateTime? ExpiresOn,
    string? Reason) : IRequest<DelegationDto>;

internal sealed class GrantDelegationCommandHandler(
    IDelegationRepository delegations,
    AuthorizationGuard guard,
    IOptions<AuthorizationOptions> options,
    IMediator mediator) : IRequestHandler<GrantDelegationCommand, DelegationDto>
{
    public async Task<DelegationDto> Handle(GrantDelegationCommand request, CancellationToken ct)
    {
        var scope = AuthorizationScopeParser.Parse(request.ScopeType, request.ScopeId, request.ResourceType);

        await guard.RequireAsync(
            request.ActorId,
            PermissionCatalog.AuthzDelegationGrant,
            AuthorizationScopeParser.ToContext(scope),
            ct);

        if (request.Permissions.Count > options.Value.MaxDelegatedPermissions)
            throw new InvalidDelegationRequestException(
                $"A delegation may grant at most {options.Value.MaxDelegatedPermissions} permissions.");

        var from = request.StartsOn ?? DateTime.UtcNow;
        var expiresOn = request.ExpiresOn ?? from.AddDays(options.Value.MaxDelegationDurationDays);

        if (expiresOn - from > TimeSpan.FromDays(options.Value.MaxDelegationDurationDays))
            throw new InvalidDelegationRequestException(
                $"A delegation may not exceed {options.Value.MaxDelegationDurationDays} days.");

        // Privilege-escalation protection: the delegator must hold each
        // delegated permission at the delegation scope. A delegator can never
        // grant more than they hold.
        foreach (var permission in request.Permissions)
        {
            if (!await guard.HasAsync(request.ActorId, permission, AuthorizationScopeParser.ToContext(scope), ct))
                throw new AuthorizationForbiddenException(permission);
        }

        var delegation = Delegation.Create(
            request.ActorId,
            request.DelegateId,
            request.Permissions,
            scope,
            from,
            expiresOn,
            request.Reason);

        await DomainEventPublisher.PublishAsync(delegation, mediator, ct);
        await delegations.AddAsync(delegation, ct);

        return Map(delegation);
    }

    internal static DelegationDto Map(Delegation delegation) => new(
        delegation.Id,
        delegation.DelegatorId,
        delegation.DelegateId,
        delegation.Permissions,
        delegation.Scope.Type.Name,
        delegation.Scope.ScopeId,
        delegation.Scope.ResourceType,
        delegation.StartsOn,
        delegation.ExpiresOn,
        delegation.Reason,
        delegation.IsRevoked);
}

public sealed record RevokeDelegationCommand(
    Guid ActorId,
    Guid DelegationId,
    string? Reason) : IRequest<DelegationDto>;

internal sealed class RevokeDelegationCommandHandler(
    IDelegationRepository delegations,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<RevokeDelegationCommand, DelegationDto>
{
    public async Task<DelegationDto> Handle(RevokeDelegationCommand request, CancellationToken ct)
    {
        var delegation = await delegations.GetByIdAsync(request.DelegationId, ct)
            ?? throw new DelegationNotFoundException(request.DelegationId);

        // The delegator may always revoke their own delegation; anyone else
        // needs the management capability at the delegation scope.
        if (request.ActorId != delegation.DelegatorId)
        {
            await guard.RequireAsync(
                request.ActorId,
                PermissionCatalog.AuthzDelegationRevoke,
                AuthorizationScopeParser.ToContext(delegation.Scope),
                ct);
        }

        delegation.Revoke(request.ActorId, request.Reason);

        await DomainEventPublisher.PublishAsync(delegation, mediator, ct);
        await delegations.UpdateAsync(delegation, ct);

        return GrantDelegationCommandHandler.Map(delegation);
    }
}
