using CommunityOS.Host.ApiGateway.Forwarding;

namespace CommunityOS.Host.ApiGateway.Extensions;

/// <summary>
/// Pipeline and endpoint wiring for the API Gateway (ADR-035). The Gateway
/// exposes <c>/health</c> and a single transparent forwarding catch-all. No
/// authentication or authorization middleware is configured: the Gateway never
/// validates JWTs, never makes allow/deny decisions, and never calls an
/// authorization endpoint — downstream services remain authoritative.
/// </summary>
internal static class WebApplicationExtensions
{
    internal static WebApplication MapGatewayPipeline(this WebApplication app)
    {
        app.MapHealthChecks("/health");

        app.Map("{**path}", async (HttpContext context, GatewayForwarder forwarder, CancellationToken ct) =>
        {
            var path = Normalize(context.Request.RouteValues["path"] as string);

            if (!GatewayRouteTable.TryResolve(path, out _) ||
                GatewayRouteTable.IsInternalRoute(path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await using var result = await forwarder.ForwardAsync(context, path, ct);
            if (result is null)
            {
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                return;
            }

            await WriteResultAsync(context, result, ct);
        });

        return app;
    }

    private static string Normalize(string? routePath)
    {
        if (string.IsNullOrEmpty(routePath))
            return "/";
        return routePath.StartsWith('/') ? routePath : "/" + routePath;
    }

    private static async Task WriteResultAsync(
        HttpContext context, GatewayForwardResponse result, CancellationToken ct)
    {
        context.Response.StatusCode = (int)result.StatusCode;

        foreach (var header in result.ResponseHeaders)
            context.Response.Headers[header.Key] = header.Value.ToArray();

        context.Response.Headers["X-Request-Id"] = result.RequestId;

        if (result.ContentType is not null)
            context.Response.ContentType = result.ContentType;

        if (result.ContentStream is { } stream)
            await stream.CopyToAsync(context.Response.Body, ct);
    }
}
