using CommunityOS.Authorization.Application.Authorization;

namespace CommunityOS.Authorization.Application.Interfaces;

/// <summary>
/// Evaluates authorization requests and returns a fail-closed decision.
/// The abstraction exists so CommunityOS can later adopt a dedicated
/// authorization engine (e.g. OpenFGA, SpiceDB, OPA) without changing the
/// consumers. See ADR candidate for the unresolved engine selection.
/// </summary>
public interface IAuthorizationEvaluator
{
    Task<AuthorizationDecision> EvaluateAsync(AuthorizationRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AuthorizationDecision>> EvaluateBatchAsync(
        IReadOnlyList<AuthorizationRequest> requests, CancellationToken ct = default);
}
