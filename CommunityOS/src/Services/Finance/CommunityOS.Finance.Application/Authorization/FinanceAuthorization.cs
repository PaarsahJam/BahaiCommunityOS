using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Finance.Domain.Aggregates;
using CommunityOS.Finance.Domain.Exceptions;

namespace CommunityOS.Finance.Application.Authorization;

/// <summary>
/// Builds the resource-level authorization contexts for funds and ledger
/// entries (ADR-032). The fund is the authorization resource: every finance
/// permission is evaluated at a fund's organizational scope — with the fund id
/// attached on named resources so relation-grants and scoped grants compose
/// exactly as in other services. Finance never reads the Authorization
/// database; all decisions are delegated over HTTP (ADR-018/019).
/// </summary>
internal static class FinanceAuthorization
{
    /// <summary>Global create/list context for a given resource type, or a unit-scoped context when one is supplied.</summary>
    public static AuthorizationContext ResourceContext(string resourceType, Guid? organizationUnitId = null) =>
        organizationUnitId is { } unitId
            ? new AuthorizationContext(unitId, resourceType)
            : new AuthorizationContext(ResourceType: resourceType);

    /// <summary>A fund-scoped context, used for both fund and ledger-entry operations.</summary>
    public static AuthorizationContext ForFund(Fund fund) =>
        new(fund.OrganizationUnitId, "fund", fund.Id);

    /// <summary>
    /// The authorization context for a ledger entry is its owning fund's
    /// resource: every finance permission is granted and evaluated at the fund
    /// scope, so a transaction never carries its own resource id.
    /// </summary>
    public static AuthorizationContext ForTransaction(FinancialTransaction tx) =>
        tx.OrganizationUnitId is { } unitId
            ? new AuthorizationContext(unitId, "fund", tx.FundId)
            : new AuthorizationContext(ResourceType: "fund", ResourceId: tx.FundId);

    /// <summary>
    /// True when the caller holds <paramref name="permission"/> at the supplied
    /// context. Loaded-vs-granted reads always fail closed to 404 in the caller
    /// (no existence oracle).
    /// </summary>
    public static async Task<bool> HasAtAsync(
        AuthorizationGuard guard,
        Guid actorId,
        string permission,
        AuthorizationContext context,
        CancellationToken ct) =>
        await guard.HasAsync(actorId, permission, context, ct);

    /// <summary>
    /// True when the caller holds <paramref name="permission"/> at the fund's
    /// organizational scope.
    /// </summary>
    public static async Task<bool> HasForFundAsync(
        AuthorizationGuard guard,
        Guid actorId,
        string permission,
        Fund fund,
        CancellationToken ct) =>
        await HasAtAsync(guard, actorId, permission, ForFund(fund), ct);

    /// <summary>
    /// Throws <see cref="AuthorizationForbiddenException"/> when the caller
    /// does not hold <paramref name="permission"/> at the supplied context.
    /// </summary>
    public static async Task RequireAtAsync(
        AuthorizationGuard guard,
        Guid actorId,
        string permission,
        AuthorizationContext context,
        CancellationToken ct)
    {
        if (!await HasAtAsync(guard, actorId, permission, context, ct))
            throw new AuthorizationForbiddenException(permission);
    }

    /// <summary>
    /// Throws <see cref="AuthorizationForbiddenException"/> when the caller
    /// does not hold <paramref name="permission"/> at the fund's scope.
    /// </summary>
    public static async Task RequireForFundAsync(
        AuthorizationGuard guard,
        Guid actorId,
        string permission,
        Fund fund,
        CancellationToken ct)
    {
        if (!await HasForFundAsync(guard, actorId, permission, fund, ct))
            throw new AuthorizationForbiddenException(permission);
    }
}