using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Authorization.Domain.Enumerations;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Authorization.Domain.ValueObjects;

/// <summary>
/// Describes where an authorization grant applies. A grant scoped to a
/// specific organization unit, committee or resource never automatically
/// cascades to other scopes; a global grant only ever applies to checks made
/// with no organization context. Organizational hierarchy awareness is
/// supplied by the Organization integration boundary, never duplicated here.
/// </summary>
public sealed class AuthorizationScope : ValueObject
{
    private AuthorizationScope(ScopeType type, Guid? scopeId, string? resourceType)
    {
        Type = type;
        ScopeId = scopeId;
        ResourceType = resourceType;
    }

    public ScopeType Type { get; }
    public Guid? ScopeId { get; }
    public string? ResourceType { get; }

    public bool IsGlobal => Type == ScopeType.Global;

    public static AuthorizationScope Global() => new(ScopeType.Global, null, null);

    public static AuthorizationScope Scoped(ScopeType type, Guid scopeId)
    {
        if (type == ScopeType.Global || type == ScopeType.Resource)
            throw new ArgumentException(
                $"Use {nameof(Global)} or {nameof(Resource)} for scope type '{type.Name}'.", nameof(type));

        Guard.NotDefault(scopeId, nameof(scopeId));
        return new AuthorizationScope(type, scopeId, null);
    }

    public static AuthorizationScope Resource(string resourceType, Guid resourceId)
    {
        Guard.NotNullOrWhiteSpace(resourceType, nameof(resourceType));
        Guard.NotDefault(resourceId, nameof(resourceId));
        return new AuthorizationScope(ScopeType.Resource, resourceId, resourceType.Trim());
    }

    /// <summary>
    /// Determines whether this grant's scope covers the requested target scope.
    /// Exact-match only; hierarchical organization knowledge is intentionally
    /// NOT embedded here (Organization service owns that data).
    /// </summary>
    public bool AppliesTo(AuthorizationScope target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (IsGlobal)
            return target.IsGlobal;

        if (target.IsGlobal)
            return false;

        if (Type == ScopeType.Resource)
            return target.Type == ScopeType.Resource &&
                   string.Equals(ResourceType, target.ResourceType, StringComparison.Ordinal) &&
                   ScopeId == target.ScopeId;

        return Type == target.Type && ScopeId == target.ScopeId;
    }

    /// <summary>
    /// True when this scope is an organization-unit style scope (national,
    /// regional, local, org unit or committee) that may participate in
    /// hierarchical matching via the Organization integration boundary.
    /// </summary>
    public bool IsOrganizationScoped =>
        Type == ScopeType.National ||
        Type == ScopeType.Regional ||
        Type == ScopeType.Local ||
        Type == ScopeType.OrganizationUnit ||
        Type == ScopeType.Committee;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return ScopeId;
        yield return ResourceType;
    }

    public override string ToString() =>
        IsGlobal ? Type.Name : Type.Name + ":" + ScopeId +
        (Type == ScopeType.Resource && ResourceType is not null ? ":" + ResourceType : string.Empty);
}
