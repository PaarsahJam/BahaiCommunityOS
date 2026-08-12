namespace CommunityOS.Organization.API.Config;

/// <summary>
/// Operational configuration for the Organization API host.
/// </summary>
public sealed class OrganizationApiOptions
{
    public const string SectionName = "Organization";

    /// <summary>
    /// Identifier presented in the <c>X-Client-Id</c> header by trusted
    /// internal callers (currently the Authorization service's organization
    /// context provider). Only this client may query the narrow coverage fact
    /// endpoint. Fail closed: anything else is rejected.
    /// </summary>
    public string InternalClientId { get; set; } = "communityos-authorization";
}
