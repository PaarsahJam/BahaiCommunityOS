using System.Net;
using System.Net.Http.Headers;

namespace CommunityOS.Host.ApiGateway.Forwarding;

/// <summary>
/// The result of a transparent Gateway forward (ADR-035). It owns the downstream
/// <see cref="HttpResponseMessage"/> and exposes the response surface the
/// Gateway preserves: HTTP status, content type, response body stream and
/// select response headers. Disposing this object releases the downstream
/// response.
/// </summary>
public sealed class GatewayForwardResponse : IDisposable, IAsyncDisposable
{
    private readonly HttpResponseMessage _response;
    private bool _disposed;

    public GatewayForwardResponse(HttpResponseMessage response, string requestId)
    {
        _response = response;
        RequestId = requestId;
    }

    /// <summary>Downstream HTTP status code.</summary>
    public HttpStatusCode StatusCode => _response.StatusCode;

    /// <summary>Downstream response content type, when present.</summary>
    public string? ContentType =>
        _response.Content.Headers.ContentType?.ToString();

    /// <summary>Downstream response body stream, when present.</summary>
    public Stream? ContentStream
    {
        get
        {
            ThrowIfDisposed();
            return _response.Content is { } content &&
                   content.Headers.ContentLength is not 0
                ? content.ReadAsStream()
                : null;
        }
    }

    /// <summary>Correlation/request ID propagated for this forward.</summary>
    public string RequestId { get; }

    /// <summary>
    /// Enumerates the headers to surface back to the caller, excluding
    /// hop-by-hop headers and content framing that the Gateway recomputes.
    /// </summary>
    public IEnumerable<KeyValuePair<string, IEnumerable<string>>> ResponseHeaders
    {
        get
        {
            ThrowIfDisposed();
            return EnumerateResponseHeaders();
        }
    }

    private IEnumerable<KeyValuePair<string, IEnumerable<string>>> EnumerateResponseHeaders()
    {
        foreach (var header in _response.Headers)
        {
            if (header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase))
                continue;
            yield return new KeyValuePair<string, IEnumerable<string>>(
                header.Key, header.Value.ToArray());
        }

        foreach (var header in _response.Content.Headers)
        {
            if (HopByHop(header.Key) ||
                header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                continue;
            yield return new KeyValuePair<string, IEnumerable<string>>(
                header.Key, header.Value.ToArray());
        }
    }

    private static bool HopByHop(string name) => name.Equals(
        "Content-Length", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Connection", StringComparison.OrdinalIgnoreCase);

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _response.Dispose();
        _disposed = true;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
