using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Notifications.Infrastructure.Integration.Community;

/// <summary>
/// Configuration for the Notifications → Community service integration (ADR-025,
/// decision 5). Recipients are stable member ids; channel destinations are
/// resolved through the Community API at dispatch time and are never stored.
/// InApp needs no destination resolution, so an unset base URL never blocks
/// InApp dispatch; Email/SMS/Push dispatch fails closed at dispatch time.
/// </summary>
public sealed class CommunityServiceOptions
{
    public const string SectionName = "CommunityService";

    /// <summary>Base URL of the Community service API.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Bearer service token Notifications presents to the Community service.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Client identifier sent as the <c>X-Client-Id</c> header.</summary>
    public string ClientId { get; set; } = "communityos-notifications";
}

public static class NotificationsCommunityServiceOptionsExtensions
{
    public static IServiceCollection ConfigureNotificationsCommunityService(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<CommunityServiceOptions>()
            .Bind(config.GetSection(CommunityServiceOptions.SectionName));
        return services;
    }
}