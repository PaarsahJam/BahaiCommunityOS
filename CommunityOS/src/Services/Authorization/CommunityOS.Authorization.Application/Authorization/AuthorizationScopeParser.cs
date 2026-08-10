using CommunityOS.Authorization.Domain.Enumerations;
using CommunityOS.Authorization.Domain.ValueObjects;

namespace CommunityOS.Authorization.Application.Authorization;

/// <summary>
/// Parses untrusted scope payloads (transport strings) into trusted domain
/// value objects and derives guard contexts from scopes. All parsing fails
/// closed on unknown scope types.
/// </summary>
public static class AuthorizationScopeParser
{
    public static AuthorizationScope Parse(string scopeType, Guid? scopeId, string? resourceType)
    {
        var type = ScopeType.All.FirstOrDefault(
            t => string.Equals(t.Name, scopeType, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Unknown scope type '{scopeType}'.", nameof(scopeType));

        return type switch
        {
            _ when type == ScopeType.Global => AuthorizationScope.Global(),
            _ when type == ScopeType.Resource => AuthorizationScope.Resource(
                resourceType ?? throw new ArgumentException("Resource scope requires a resource type.", nameof(resourceType)),
                scopeId ?? throw new ArgumentException("Resource scope requires a scope id.", nameof(scopeId))),
            _ => AuthorizationScope.Scoped(
                type,
                scopeId ?? throw new ArgumentException($"Scope type '{type.Name}' requires a scope id.", nameof(scopeId)))
        };
    }

    public static AuthorizationContext ToContext(AuthorizationScope scope) =>
        scope.Type == ScopeType.Resource
            ? new AuthorizationContext(ResourceType: scope.ResourceType, ResourceId: scope.ScopeId)
            : scope.IsGlobal
                ? AuthorizationContext.Empty
                : new AuthorizationContext(OrganizationUnitId: scope.ScopeId);
}
