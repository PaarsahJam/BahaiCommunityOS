using CommunityOS.Host.ApiGateway.Configuration;
using Microsoft.Extensions.Options;

namespace CommunityOS.Host.ApiGateway.Forwarding;

/// <summary>
/// Dedicated transparent forwarding abstraction for the API Gateway (ADR-035).
/// This type is intentionally NOT a service-to-service client: it copies the
/// caller's incoming <c>Authorization</c> header verbatim and never substitutes
/// a Gateway or service credential. The Gateway does not authenticate, does not
/// authorize, does not mint/exchange/refresh tokens, and does not call any
/// authorization endpoint; downstream services remain the authentication and
/// authorization authorities.
/// </summary>
public sealed class GatewayForwarder(
    IHttpClientFactory httpClientFactory,
    IOptions<GatewayOptions> gatewayOptions,
    ILogger<GatewayForwarder> logger)
{
    private const string HttpClientName = "Downstream";

    private static readonly HashSet<string> HopByHopHeaders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Connection",
            "Keep-Alive",
            "Proxy-Authenticate",
            "Proxy-Authorization",
            "TE",
            "Trailer",
            "Transfer-Encoding",
            "Upgrade",
            "Host",
            "Content-Length",
            "Accept-Encoding"
        };

    /// <summary>
    /// Internal service-identity headers that must never be accepted from or
    /// forwarded on behalf of public clients (ADR-035). The Gateway must not
    /// expose or require <c>X-Client-Id</c> from external callers.
    /// </summary>
    private static readonly HashSet<string> InternalIdentityHeaders =
        new(StringComparer.OrdinalIgnoreCase) { "X-Client-Id" };

    /// <summary>
    /// Forwards a single public request to its downstream service, preserving
    /// the access-token <c>Authorization</c> header, HTTP method, query string
    /// and request body. The caller owns the returned response and must dispose
    /// it. Returns <see langword="null"/> when the request cannot be resolved or
    /// the target cannot be reached (a Gateway transport failure).
    /// </summary>
    public async Task<GatewayForwardResponse?> ForwardAsync(
        HttpContext context, string path, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!GatewayRouteTable.TryResolve(path, out var service) ||
            GatewayRouteTable.IsInternalRoute(path))
            return null;

        var baseUrl = ResolveBaseUrl(service);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.DownstreamBaseUrlNotConfigured(service);
            return null;
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        var request = await BuildDownstreamRequestAsync(context, baseUrl, path, ct);
        await CopyRequestHeadersAsync(context.Request, request, service, ct);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.TransportFailure(ex, service);
            return null;
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }

        return new GatewayForwardResponse(response, context.TraceIdentifier);
    }

    private string ResolveBaseUrl(DownstreamService service) =>
        service switch
        {
            DownstreamService.Identity => gatewayOptions.Value.Identity.BaseUrl,
            DownstreamService.Authorization => gatewayOptions.Value.Authorization.BaseUrl,
            DownstreamService.Organization => gatewayOptions.Value.Organization.BaseUrl,
            DownstreamService.Community => gatewayOptions.Value.Community.BaseUrl,
            DownstreamService.Notifications => gatewayOptions.Value.Notifications.BaseUrl,
            _ => string.Empty
        };

    private static async Task<HttpRequestMessage> BuildDownstreamRequestAsync(
        HttpContext context, string baseUrl, string path, CancellationToken ct)
    {
        var target = baseUrl.TrimEnd('/') + path + context.Request.QueryString.Value;
        var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), target);

        if (context.Request.ContentLength is not null || context.Request.Body is not null)
        {
            using var buffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(buffer, ct);
            if (buffer.Length > 0)
            {
                request.Content = new ByteArrayContent(buffer.ToArray());
                if (context.Request.ContentType is not null)
                    request.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
            }
        }

        return request;
    }

    private static async Task CopyRequestHeadersAsync(
        HttpRequest source, HttpRequestMessage target,
        DownstreamService service, CancellationToken ct)
    {
        foreach (var header in source.Headers)
        {
            if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                // Preserve the caller's access token unchanged (the single most
                // important Gateway boundary). Never replaced with a service
                // credential and never interpreted as an authorization decision.
                foreach (var value in header.Value)
                    target.Headers.TryAddWithoutValidation("Authorization", value);
                continue;
            }

            if (HopByHopHeaders.Contains(header.Key) ||
                InternalIdentityHeaders.Contains(header.Key))
                continue;

            foreach (var value in header.Value)
                target.Headers.TryAddWithoutValidation(header.Key, value);
        }

        // Request/correlation propagation (ADR-035): forward the Gateway
        // request/request-ID so downstream services share the trace.
        target.Headers.TryAddWithoutValidation("X-Request-Id", source.HttpContext.TraceIdentifier);
    }
}
