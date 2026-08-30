using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.DTOs;
using CommunityOS.Authorization.Application.Permissions;
using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Enumerations;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Authorization.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Options;

namespace CommunityOS.Authorization.Application.Commands;

public sealed record RequestBreakGlassCommand(
    Guid ActorId,
    IReadOnlyList<string> Permissions,
    string ScopeType,
    Guid? ScopeId,
    string? ResourceType,
    string Reason,
    int RequestedDurationMinutes) : IRequest<BreakGlassRequestDto>;

internal sealed class RequestBreakGlassCommandHandler(
    IBreakGlassRequestRepository requests,
    IOptions<AuthorizationOptions> options,
    IMediator mediator) : IRequestHandler<RequestBreakGlassCommand, BreakGlassRequestDto>
{
    public async Task<BreakGlassRequestDto> Handle(RequestBreakGlassCommand request, CancellationToken ct)
    {
        var scope = AuthorizationScopeParser.Parse(request.ScopeType, request.ScopeId, request.ResourceType);
        if (scope.IsGlobal)
            throw new BreakGlassGlobalScopeForbiddenException();

        var maxMinutes = options.Value.MaxBreakGlassDurationMinutes;
        if (request.RequestedDurationMinutes <= 0 || request.RequestedDurationMinutes > maxMinutes)
            throw new InvalidBreakGlassRequestException(
                $"A break-glass request duration must be between 1 and {maxMinutes} minutes.");

        if (request.Permissions.Count > options.Value.MaxBreakGlassPermissions)
            throw new InvalidBreakGlassRequestException(
                $"A break-glass request is limited to {options.Value.MaxBreakGlassPermissions} permissions.");

        var requestEntity = BreakGlassRequest.Create(
            request.ActorId,
            scope,
            request.Permissions,
            request.Reason,
            TimeSpan.FromMinutes(request.RequestedDurationMinutes),
            DateTime.UtcNow);

        await DomainEventPublisher.PublishAsync(requestEntity, mediator, ct);
        await requests.AddAsync(requestEntity, ct);

        return Map(requestEntity);
    }

    internal static BreakGlassRequestDto Map(BreakGlassRequest request) => new(
        request.Id,
        request.RequesterId,
        request.State.Name,
        request.Permissions,
        request.Scope.Type.Name,
        request.Scope.ScopeId,
        request.Scope.ResourceType,
        request.Reason,
        request.RequestedAt,
        (int)Math.Round(request.RequestedDuration.TotalMinutes, MidpointRounding.AwayFromZero),
        request.ApproverId,
        request.ApprovedOn,
        request.ApprovedUntil,
        request.RejectionReason,
        request.RevokedBy,
        request.RevokedAt,
        request.RevocationReason);
}

public sealed record ApproveBreakGlassCommand(
    Guid ActorId,
    Guid RequestId) : IRequest<BreakGlassRequestDto>;

internal sealed class ApproveBreakGlassCommandHandler(
    IBreakGlassRequestRepository requests,
    AuthorizationGuard guard,
    IOptions<AuthorizationOptions> options,
    IMediator mediator) : IRequestHandler<ApproveBreakGlassCommand, BreakGlassRequestDto>
{
    public async Task<BreakGlassRequestDto> Handle(ApproveBreakGlassCommand request, CancellationToken ct)
    {
        var requestEntity = await requests.GetByIdAsync(request.RequestId, ct)
            ?? throw new BreakGlassRequestNotFoundException(request.RequestId);

        await guard.RequireAsync(
            request.ActorId,
            PermissionCatalog.AuthzBreakGlassApprove,
            AuthorizationScopeParser.ToContext(requestEntity.Scope),
            ct);

        var maxMinutes = options.Value.MaxBreakGlassDurationMinutes;
        if (requestEntity.RequestedDuration > TimeSpan.FromMinutes(maxMinutes))
            throw new InvalidBreakGlassRequestException(
                $"The requested duration exceeds the configured maximum of {maxMinutes} minutes.");

        requestEntity.Approve(request.ActorId, DateTime.UtcNow, requestEntity.RequestedDuration);

        await DomainEventPublisher.PublishAsync(requestEntity, mediator, ct);
        await requests.UpdateAsync(requestEntity, ct);

        return RequestBreakGlassCommandHandler.Map(requestEntity);
    }
}

public sealed record RejectBreakGlassCommand(
    Guid ActorId,
    Guid RequestId,
    string? Reason) : IRequest<BreakGlassRequestDto>;

internal sealed class RejectBreakGlassCommandHandler(
    IBreakGlassRequestRepository requests,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<RejectBreakGlassCommand, BreakGlassRequestDto>
{
    public async Task<BreakGlassRequestDto> Handle(RejectBreakGlassCommand request, CancellationToken ct)
    {
        var requestEntity = await requests.GetByIdAsync(request.RequestId, ct)
            ?? throw new BreakGlassRequestNotFoundException(request.RequestId);

        await guard.RequireAsync(
            request.ActorId,
            PermissionCatalog.AuthzBreakGlassApprove,
            AuthorizationScopeParser.ToContext(requestEntity.Scope),
            ct);

        requestEntity.Reject(request.ActorId, request.Reason);

        await DomainEventPublisher.PublishAsync(requestEntity, mediator, ct);
        await requests.UpdateAsync(requestEntity, ct);

        return RequestBreakGlassCommandHandler.Map(requestEntity);
    }
}

public sealed record RevokeBreakGlassCommand(
    Guid ActorId,
    Guid RequestId,
    string? Reason) : IRequest<BreakGlassRequestDto>;

internal sealed class RevokeBreakGlassCommandHandler(
    IBreakGlassRequestRepository requests,
    AuthorizationGuard guard,
    IMediator mediator) : IRequestHandler<RevokeBreakGlassCommand, BreakGlassRequestDto>
{
    public async Task<BreakGlassRequestDto> Handle(RevokeBreakGlassCommand request, CancellationToken ct)
    {
        var requestEntity = await requests.GetByIdAsync(request.RequestId, ct)
            ?? throw new BreakGlassRequestNotFoundException(request.RequestId);

        // The requester may always withdraw their own request; anyone else
        // needs the management capability at the request scope.
        if (request.ActorId != requestEntity.RequesterId)
        {
            await guard.RequireAsync(
                request.ActorId,
                PermissionCatalog.AuthzBreakGlassRevoke,
                AuthorizationScopeParser.ToContext(requestEntity.Scope),
                ct);
        }

        requestEntity.Revoke(request.ActorId, request.Reason);

        await DomainEventPublisher.PublishAsync(requestEntity, mediator, ct);
        await requests.UpdateAsync(requestEntity, ct);

        return RequestBreakGlassCommandHandler.Map(requestEntity);
    }
}

public sealed record ListBreakGlassRequestsQuery(
    Guid ActorId,
    string? State) : IRequest<IReadOnlyList<BreakGlassRequestDto>>;

internal sealed class ListBreakGlassRequestsQueryHandler(
    IBreakGlassRequestRepository requests,
    AuthorizationGuard guard) : IRequestHandler<ListBreakGlassRequestsQuery, IReadOnlyList<BreakGlassRequestDto>>
{
    public async Task<IReadOnlyList<BreakGlassRequestDto>> Handle(ListBreakGlassRequestsQuery request, CancellationToken ct)
    {
        var canListAll = await guard.HasAsync(request.ActorId, PermissionCatalog.AuthzBreakGlassList, null, ct);

        IReadOnlyList<BreakGlassRequest> items = canListAll
            ? await requests.ListAllAsync(ct)
            : await requests.ListByRequesterAsync(request.ActorId, ct);

        if (request.State is null)
            return items.Select(RequestBreakGlassCommandHandler.Map).ToList();

        var state = BreakGlassRequestState.All.FirstOrDefault(
            s => string.Equals(s.Name, request.State, StringComparison.OrdinalIgnoreCase));
        if (state is null)
            return new List<BreakGlassRequestDto>();

        return items
            .Where(r => r.State == state)
            .Select(RequestBreakGlassCommandHandler.Map)
            .ToList();
    }
}

public sealed record GetRolesQuery(Guid ActorId) : IRequest<IReadOnlyList<RoleDto>>;

internal sealed class GetRolesQueryHandler(
    IRoleRepository roles,
    AuthorizationGuard guard) : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    public async Task<IReadOnlyList<RoleDto>> Handle(GetRolesQuery request, CancellationToken ct)
    {
        await guard.RequireAsync(request.ActorId, PermissionCatalog.AuthzRoleList, null, ct);

        var items = await roles.ListAsync(ct);
        return items.Select(CreateRoleCommandHandler.Map).ToList();
    }
}
