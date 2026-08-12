using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Organization.Infrastructure.Integration.Authorization;

/// <summary>
/// Configuration for the Organization → Authorization service integration.
/// The Organization service never reads the Authorization database; it calls
/// the Authorization service's check API over HTTP as a service principal.
/// </summary>
public sealed class AuthorizationServiceOptions
{
    public const string SectionName = "AuthorizationService";

    /// <summary>Base URL of the Authorization service API.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Bearer access token the Organization service presents to the
    /// Authorization service. In development this is a configured platform
    /// service token with the <c>authz.check</c> capability.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Optional identifier sent as a header for audit/tracing.</summary>
    public string ClientId { get; set; } = "communityos-organization";
}

public static class AuthorizationServiceOptionsExtensions
{
    public static IServiceCollection ConfigureAuthorizationService(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<AuthorizationServiceOptions>()
            .Bind(config.GetSection(AuthorizationServiceOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "AuthorizationService:BaseUrl is required.");
        return services;
    }
}