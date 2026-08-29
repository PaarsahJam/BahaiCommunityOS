using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CommunityOS.ServiceClients;

/// <summary>
/// Shared mechanics for internal service-to-service HTTP calls (ADR-031,
/// OQ-2 narrow scope). This type owns only the transport and service-identity
/// mechanics: URI composition from <see cref="ServiceClientOptions.BaseUrl"/>,
/// Bearer + <c>X-Client-Id</c> header population, and web-compatible JSON
/// serialization. It intentionally knows nothing about any consuming service's
/// domain, endpoints, DTOs, or failure policy — callers decide how to interpret
/// responses and how to treat transport failures.
/// </summary>
public static class ServiceHttpClient
{
    /// <summary>
    /// Shared web-compatible JSON serializer options (camelCase, case-insensitive
    /// reading) used for request/response payloads on internal service calls.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Builds an <see cref="HttpRequestMessage"/> for the given internal service
    /// call. The URI is composed as <c>options.BaseUrl</c> with any trailing
    /// slashes trimmed, followed by <paramref name="path"/>. When
    /// <paramref name="body"/> is provided it is serialized as JSON with
    /// <see cref="JsonOptions"/>. The caller is responsible for disposing the
    /// returned request and for deciding the response failure policy.
    /// </summary>
    /// <param name="options">Transport and service-identity options.</param>
    /// <param name="method">HTTP method for the call.</param>
    /// <param name="path">Absolute service path, e.g. <c>/api/v1/resource</c>.</param>
    /// <param name="body">Optional request body serialized as JSON.</param>
    /// <returns>The constructed request, not yet sent.</returns>
    public static HttpRequestMessage CreateRequest(
        ServiceClientOptions options,
        HttpMethod method,
        string path,
        object? body = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var uri = new Uri(options.BaseUrl.TrimEnd('/') + path);
        var request = new HttpRequestMessage(method, uri);

        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", options.AccessToken);
        request.Headers.TryAddWithoutValidation("X-Client-Id", options.ClientId);

        return request;
    }
}