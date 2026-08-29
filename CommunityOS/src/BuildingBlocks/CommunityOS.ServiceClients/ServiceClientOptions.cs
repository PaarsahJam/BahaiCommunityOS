namespace CommunityOS.ServiceClients;

/// <summary>
/// Common configuration surface for an internal service-to-service typed HTTP
/// client (ADR-031). Services present their machine identity to the target
/// service as a Bearer token plus a stable <c>X-Client-Id</c> value. This base
/// type carries only transport and service-identity mechanics; domain-specific
/// options remain in the owning service.
/// </summary>
public abstract class ServiceClientOptions
{
    /// <summary>Base URL of the target service API.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Bearer service token presented to the target service.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Client identifier sent as the <c>X-Client-Id</c> header.</summary>
    public string ClientId { get; set; } = string.Empty;
}