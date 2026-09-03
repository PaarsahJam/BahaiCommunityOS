namespace CommunityOS.Host.ApiGateway.Configuration;

/// <summary>
/// Root configuration surface for the API Gateway (ADR-035). Each public
/// downstream service is resolved by a static <c>BaseUrl</c> configured using
/// the repository's existing configuration/environment-override conventions.
/// No service discovery is used; the Gateway routes by a deliberate public
/// route table and forwards the caller's access token unchanged.
/// </summary>
public sealed class GatewayOptions
{
    public DownstreamServiceOptions Identity { get; set; } = new();

    public DownstreamServiceOptions Authorization { get; set; } = new();

    public DownstreamServiceOptions Organization { get; set; } = new();

    public DownstreamServiceOptions Community { get; set; } = new();
}

/// <summary>
/// Base URL of a single downstream Gateway target service.
/// </summary>
public sealed class DownstreamServiceOptions
{
    public string BaseUrl { get; set; } = string.Empty;
}
