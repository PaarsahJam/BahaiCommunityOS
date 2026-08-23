using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Correspondence.Application.Permissions;

namespace CommunityOS.Correspondence.Application.Authorization;

/// <summary>
/// Builds the resource-level authorization context for a letter (ADR-028
/// decision 15). A letter carries exactly one organization scope (its issuing
/// unit); access is granted when the caller holds the permission at that
/// scope — national/regional/local grants cover descendants through the
/// Authorization service's hierarchy resolution. Fail-closed: any inability to
/// establish the grant is a Deny.
/// </summary>
internal static class LetterAuthorization
{
    public static AuthorizationContext ContextFor(Guid letterId, Guid organizationUnitId) =>
        new(organizationUnitId, LetterPermissions.ResourceType, letterId);
}
