namespace CommunityOS.Authorization.Application.Interfaces;

/// <summary>
/// Integration boundary for organization hierarchy facts. The Organization
/// service owns organization units, hierarchy and appointments; Authorization
/// must never duplicate or query that data directly. This provider is
/// implemented from Organization versioned events / APIs and defaults to
/// exact-match only (fail closed) until that integration exists.
/// </summary>
public interface IOrganizationContextProvider
{
    /// <summary>
    /// Returns true when <paramref name="candidateAncestorId"/> is the same
    /// organization unit as, or an ancestor of, <paramref name="orgUnitId"/>.
    /// </summary>
    Task<bool> IsAncestorOrSelfAsync(
        Guid candidateAncestorId, Guid orgUnitId, CancellationToken ct = default);
}
