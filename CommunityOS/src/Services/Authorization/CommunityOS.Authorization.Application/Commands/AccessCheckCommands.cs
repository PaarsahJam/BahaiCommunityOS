using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.DTOs;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Application.Permissions;
using MediatR;

namespace CommunityOS.Authorization.Application.Commands;

public sealed record CheckPermissionCommand(
    Guid ActorId,
    Guid SubjectId,
    string Permission,
    Guid? OrganizationUnitId,
    string? ResourceType,
    Guid? ResourceId,
    string? CheckKey,
    IReadOnlyDictionary<string, string>? Attributes = null) : IRequest<AuthorizationCheckDto>;

internal sealed class CheckPermissionCommandHandler(
    IAuthorizationEvaluator evaluator,
    AuthorizationGuard guard) : IRequestHandler<CheckPermissionCommand, AuthorizationCheckDto>
{
    public async Task<AuthorizationCheckDto> Handle(CheckPermissionCommand request, CancellationToken ct)
    {
        // A check on behalf of another subject is a privileged (service)
        // operation and requires the authz.check capability.
        if (request.ActorId != request.SubjectId)
            await guard.RequireAsync(request.ActorId, PermissionCatalog.AuthzCheck, null, ct);

        var context = new AuthorizationContext(
            request.OrganizationUnitId,
            request.ResourceType,
            request.ResourceId,
            request.Attributes);

        var decision = await evaluator.EvaluateAsync(
            new AuthorizationRequest(request.SubjectId, request.Permission, context), ct);

        return new AuthorizationCheckDto(
            request.CheckKey ?? request.SubjectId.ToString("N"),
            decision.Allowed,
            decision.DecisionId,
            ReasonCode(decision.Reason),
            decision.EvaluatedOn);
    }

    internal static string ReasonCode(AuthorizationDecisionReason reason) => reason switch
    {
        AuthorizationDecisionReason.Allowed => "allowed",
        AuthorizationDecisionReason.MissingSubject => "missing_subject",
        AuthorizationDecisionReason.InvalidPermission => "invalid_permission",
        AuthorizationDecisionReason.NoPermission => "no_permission",
        AuthorizationDecisionReason.ScopeMismatch => "scope_mismatch",
        _ => "denied_by_default"
    };
}

public sealed record CheckRequestItem(
    Guid SubjectId,
    string Permission,
    Guid? OrganizationUnitId,
    string? ResourceType,
    Guid? ResourceId,
    string? CheckKey,
    IReadOnlyDictionary<string, string>? Attributes = null);

public sealed record BatchCheckPermissionCommand(
    Guid ActorId,
    IReadOnlyList<CheckRequestItem> Requests) : IRequest<BatchAuthorizationCheckDto>;

internal sealed class BatchCheckPermissionCommandHandler(
    IAuthorizationEvaluator evaluator,
    AuthorizationGuard guard) : IRequestHandler<BatchCheckPermissionCommand, BatchAuthorizationCheckDto>
{
    public async Task<BatchAuthorizationCheckDto> Handle(BatchCheckPermissionCommand request, CancellationToken ct)
    {
        if (request.Requests.Any(r => r.SubjectId != request.ActorId))
            await guard.RequireAsync(request.ActorId, PermissionCatalog.AuthzCheck, null, ct);

        var results = new List<AuthorizationCheckDto>(request.Requests.Count);
        foreach (var item in request.Requests)
        {
            var context = new AuthorizationContext(
                item.OrganizationUnitId,
                item.ResourceType,
                item.ResourceId,
                item.Attributes);

            var decision = await evaluator.EvaluateAsync(
                new AuthorizationRequest(item.SubjectId, item.Permission, context), ct);

            results.Add(new AuthorizationCheckDto(
                item.CheckKey ?? item.SubjectId.ToString("N"),
                decision.Allowed,
                decision.DecisionId,
                CheckPermissionCommandHandler.ReasonCode(decision.Reason),
                decision.EvaluatedOn));
        }

        return new BatchAuthorizationCheckDto(results);
    }
}
