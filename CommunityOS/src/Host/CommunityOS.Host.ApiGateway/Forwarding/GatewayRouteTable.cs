namespace CommunityOS.Host.ApiGateway.Forwarding;

/// <summary>
/// The deliberate public route table of the API Gateway (ADR-035). Routes map
/// 1:1 from a public path prefix to a downstream service. The table is
/// explicit rather than a blind forward of every downstream controller route:
/// unknown paths and known internal-only (service-to-service) routes are not
/// exposed through the Gateway.
/// </summary>
public static class GatewayRouteTable
{
    private static readonly Dictionary<string, DownstreamService> Segments =
        new(StringComparer.Ordinal)
        {
            // Identity (anonymous routes: auth, connect, .well-known)
            ["auth"] = DownstreamService.Identity,
            ["connect"] = DownstreamService.Identity,
            [".well-known"] = DownstreamService.Identity,
            ["me"] = DownstreamService.Identity,
            ["account"] = DownstreamService.Identity,
            ["mfa"] = DownstreamService.Identity,

            // Authorization
            ["authz"] = DownstreamService.Authorization,

            // Organization
            ["organizations"] = DownstreamService.Organization,
            ["orgunits"] = DownstreamService.Organization,
            ["committees"] = DownstreamService.Organization,
            ["appointments"] = DownstreamService.Organization,
            ["institutions"] = DownstreamService.Organization,
            ["delegations"] = DownstreamService.Organization,

            // Community
            ["my-person"] = DownstreamService.Community,
            ["persons"] = DownstreamService.Community,
            ["households"] = DownstreamService.Community,
            ["memberships"] = DownstreamService.Community,
            ["meetings"] = DownstreamService.Community,
            ["activities"] = DownstreamService.Community,
            ["community-events"] = DownstreamService.Community,
            ["calendar"] = DownstreamService.Community,
            ["communities"] = DownstreamService.Community,
            ["family-relationships"] = DownstreamService.Community,
            ["participations"] = DownstreamService.Community,
        };

    /// <summary>
    /// Identity resource segments that are served anonymously by the Identity
    /// service (ADR-035). These routes must remain anonymously reachable.
    /// </summary>
    private static readonly HashSet<string> AnonymousIdentitySegments =
        new(StringComparer.Ordinal)
        {
            "auth",
            "connect",
            ".well-known"
        };

    /// <summary>
    /// Resolves a public request path (e.g. <c>/api/v1/persons/abc</c>) to its
    /// downstream service. Returns <see langword="false"/> when the path is not
    /// part of the public route table, in which case the Gateway must not
    /// forward it.
    /// </summary>
    public static bool TryResolve(string path, out DownstreamService service)
    {
        service = default;

        if (!TrySplit(path, out var segments) || segments.Length < 3)
            return false;

        if (!string.Equals(segments[0], "api", StringComparison.Ordinal) ||
            !segments[1].StartsWith('v'))
            return false;

        return Segments.TryGetValue(segments[2], out service);
    }

    /// <summary>
    /// Identifies known internal-only (service-to-service) endpoints that must
    /// not be exposed as public client routes (ADR-035). The verified example
    /// is the Organization <c>orgunits/{id}/covers</c> fact endpoint, which is
    /// guarded by the <c>X-Client-Id</c> header for the Authorization service
    /// only.
    /// </summary>
    public static bool IsInternalRoute(string path)
    {
        if (!TrySplit(path, out var segments) || segments.Length < 5)
            return false;

        return string.Equals(segments[2], "orgunits", StringComparison.Ordinal) &&
               string.Equals(segments[4], "covers", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns whether the given resource segment belongs to the set of
    /// Identity endpoints that are reachable anonymously (ADR-035).
    /// </summary>
    public static bool IsAnonymousIdentitySegment(string segment) =>
        AnonymousIdentitySegments.Contains(segment);

    private static bool TrySplit(string path, out string[] segments)
    {
        segments = path.Split('/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length > 0;
    }
}
