using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace CommunityOS.Host.ApiGateway.Tests.TestDoubles;

/// <summary>
/// In-memory downstream handler used to observe and respond to Gateway
/// forwarding without any network or Docker dependency (ADR-035 tests).
/// </summary>
public sealed class RecordingDownstreamHandler : HttpMessageHandler
{
    public ConcurrentQueue<HttpRequestMessage> Requests { get; } = new();

    public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } =
        _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"ok\":true}", Encoding.UTF8, "application/json")
        };

    public IReadOnlyList<HttpRequestMessage> CapturedRequests => Requests.ToArray();

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Enqueue(request);
        return Task.FromResult(Responder(request));
    }
}
