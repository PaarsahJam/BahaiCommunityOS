namespace CommunityOS.Host.ApiGateway.Forwarding;

/// <summary>
/// The logical downstream services reachable through the public Gateway
/// boundary (ADR-035).
/// </summary>
public enum DownstreamService
{
    Identity,
    Authorization,
    Organization,
    Community,
    Notifications
}
