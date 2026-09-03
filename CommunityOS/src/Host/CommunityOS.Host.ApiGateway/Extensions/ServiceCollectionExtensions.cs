using CommunityOS.Host.ApiGateway.Configuration;
using CommunityOS.Host.ApiGateway.Forwarding;
using Microsoft.Extensions.Options;

namespace CommunityOS.Host.ApiGateway.Extensions;

/// <summary>
/// DI registration for the API Gateway (ADR-035): configuration binding for
/// the downstream service base URLs, a resilient outbound HTTP client (no
/// service discovery), and the transparent forwarding abstraction.
/// </summary>
internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddGatewayServices(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<GatewayOptions>()
            .Bind(config.GetSection("Services"))
            .Validate(ValidateBaseUrls, "All Gateway downstream BaseUrl values must be configured.")
            .ValidateOnStart();

        services.AddHttpClient("Downstream")
            .AddStandardResilienceHandler();

        services.AddTransient<GatewayForwarder>();

        services.AddHealthChecks();

        return services;
    }

    private static bool ValidateBaseUrls(GatewayOptions options) =>
        !string.IsNullOrWhiteSpace(options.Identity.BaseUrl) &&
        !string.IsNullOrWhiteSpace(options.Authorization.BaseUrl) &&
        !string.IsNullOrWhiteSpace(options.Organization.BaseUrl) &&
        !string.IsNullOrWhiteSpace(options.Community.BaseUrl);
}
