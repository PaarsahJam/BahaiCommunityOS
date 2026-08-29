using CommunityOS.ServiceClients;

namespace CommunityOS.Authorization.Infrastructure.Integration.Organization;

/// <summary>
/// Configuration for the Authorization → Organization service integration.
/// The Authorization service never reads the Organization database; it calls
/// the Organization service's covers endpoint over HTTP as a service principal
/// to resolve organization-scoped authorization (ADR-018).
/// </summary>
public sealed class OrganizationServiceOptions : ServiceClientOptions
{
    public const string SectionName = "OrganizationService";

    public OrganizationServiceOptions()
    {
        ClientId = "communityos-authorization";
    }
}