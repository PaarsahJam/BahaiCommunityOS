using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Knowledge.Infrastructure.Integration.Authorization;

/// <summary>
/// Configuration for the Knowledge → Authorization service integration.
/// Knowledge never reads the Authorization database; every authorization
/// decision is delegated to the Authorization service's check endpoint over
/// HTTP as a service principal (ADR-018/ADR-019).
/// </summary>
public sealed class AuthorizationServiceOptions
{
    public const string SectionName = "AuthorizationService";

    /// <summary>Base URL of the Authorization service API.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Bearer access token Knowledge presents to the Authorization service.
    /// In development this is a configured platform service token.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Client identifier sent as the <c>X-Client-Id</c> header.
    /// </summary>
    public string ClientId { get; set; } = "communityos-knowledge";
}

public static class AuthorizationServiceOptionsExtensions
{
    public static IServiceCollection ConfigureAuthorizationService(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<AuthorizationServiceOptions>()
            .Bind(config.GetSection(AuthorizationServiceOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl),
                "AuthorizationService:BaseUrl is required.");
        return services;
    }
}