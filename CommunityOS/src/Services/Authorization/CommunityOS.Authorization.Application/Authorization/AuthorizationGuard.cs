using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Domain.Exceptions;

namespace CommunityOS.Authorization.Application.Authorization;

/// <summary>
/// Convenience facade over the evaluator for imperative checks. Guards are
/// used at trust boundaries inside the API and other in-process callers.
/// </summary>
public sealed class AuthorizationGuard(IAuthorizationEvaluator evaluator)
{
    public async Task<bool> HasAsync(
        Guid actorId,
        string permission,
        AuthorizationContext? context = null,
        CancellationToken ct = default)
    {
        var decision = await evaluator.EvaluateAsync(
            new AuthorizationRequest(actorId, permission, context ?? AuthorizationContext.Empty), ct);
        return decision.Allowed;
    }

    public async Task RequireAsync(
        Guid actorId,
        string permission,
        AuthorizationContext? context = null,
        CancellationToken ct = default)
    {
        if (!await HasAsync(actorId, permission, context, ct))
            throw new AuthorizationForbiddenException(permission);
    }
}
