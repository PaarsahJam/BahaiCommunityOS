using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Authorization.Infrastructure.Integration.Organization;

/// <summary>
/// Configuration for the Authorization → Organization service integration.
/// The Authorization service never reads the Organization database; it calls
/// the Organization service's covers endpoint over HTTP as a service principal
/// to resolve organization-scoped authorization (ADR-018).
/// </summary>
public sealed class OrganizationServiceOptions
{
    public const string SectionName = "OrganizationService";

    /// <summary>Base URL of the Organization service API.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Bearer access token the Authorization service presents to the
    /// Organization service. In development this is a configured platform
    /// service token issued by the Identity service.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Client identifier sent as the <c>X-Client-Id</c> header. The
    /// Organization service only accepts the internal client id below for its
    /// narrow covers endpoint.
    /// </summary>
    public string ClientId { get; set; } = "communityos-authorization";
}

public static class OrganizationServiceOptionsExtensions
{
    public static IServiceCollection ConfigureOrganizationService(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<OrganizationServiceOptions>()
            .Bind(config.GetSection(OrganizationServiceOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "OrganizationService:BaseUrl is required.");
        return services;
    }
}