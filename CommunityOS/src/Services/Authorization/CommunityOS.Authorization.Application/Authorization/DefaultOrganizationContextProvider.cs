using CommunityOS.Authorization.Application.Interfaces;

namespace CommunityOS.Authorization.Application.Authorization;

/// <summary>
/// Default organization context provider used until the Organization service
/// integration boundary is implemented. It only recognizes exact matches,
/// meaning organization hierarchy is not assumed and checks fail closed for
/// scopes that differ.
/// </summary>
public sealed class DefaultOrganizationContextProvider : IOrganizationContextProvider
{
    public Task<bool> IsAncestorOrSelfAsync(
        Guid candidateAncestorId, Guid orgUnitId, CancellationToken ct = default) =>
        Task.FromResult(candidateAncestorId == orgUnitId);
}
