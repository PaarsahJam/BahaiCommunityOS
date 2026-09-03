namespace CommunityOS.Host.ApiGateway.Tests.TestDoubles;

/// <summary>
/// <see cref="IHttpClientFactory"/> that returns clients backed by a single
/// recording downstream handler so the Gateway can be tested without network.
/// </summary>
public sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly RecordingDownstreamHandler _handler;

    public StubHttpClientFactory(RecordingDownstreamHandler handler) => _handler = handler;

    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}
