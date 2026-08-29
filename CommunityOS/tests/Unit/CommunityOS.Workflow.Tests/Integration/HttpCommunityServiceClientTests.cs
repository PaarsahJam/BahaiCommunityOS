using System.Net;
using System.Net.Http.Headers;
using CommunityOS.Workflow.Infrastructure.Integration.Community;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CommunityOS.Workflow.Tests.Integration;

/// <summary>
/// Community read surface (<c>GET /persons/{id}</c>) preserves fail-closed
/// throw-on-failure semantics for everything except the documented 404 → false
/// case (ADR-024). Uses the real typed client with a stub message handler.
/// </summary>
public class HttpCommunityServiceClientTests
{
    private static readonly Guid PersonId = Guid.NewGuid();

    private static HttpCommunityServiceClient Client(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
    {
        var handler = new StubHandler(responder);
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new CommunityServiceOptions
        {
            BaseUrl = "http://community.example",
            AccessToken = "workflow-token"
        });
        return new HttpCommunityServiceClient(httpClient, options, NullLogger<HttpCommunityServiceClient>.Instance);
    }

    [Fact]
    public async Task PersonExistsAsync_WhenNotFound_ReturnsFalse()
    {
        var client = Client(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var exists = await client.PersonExistsAsync(PersonId);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task PersonExistsAsync_WhenServerError_Throws()
    {
        var client = Client(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        Func<Task> act = () => client.PersonExistsAsync(PersonId);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task PersonExistsAsync_WhenOk_ReturnsTrue()
    {
        var client = Client(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var exists = await client.PersonExistsAsync(PersonId);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task PersonExistsAsync_OnTransportFailure_Throws()
    {
        var client = Client(_ => Task.FromException<HttpResponseMessage>(new HttpRequestException("unreachable")));

        Func<Task> act = () => client.PersonExistsAsync(PersonId);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task PersonExistsAsync_OnSuccess_SendsExpectedRequest()
    {
        HttpRequestMessage? captured = null;
        var client = Client(request =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        await client.PersonExistsAsync(PersonId);

        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri.Should().Be(new Uri($"http://community.example/api/v1/persons/{PersonId}"));
        captured.Headers.Authorization.Should().Be(
            new AuthenticationHeaderValue("Bearer", "workflow-token"));
        captured.Headers.TryGetValues("X-Client-Id", out var values).Should().BeTrue();
        values.Should().ContainSingle().Which.Should().Be("communityos-workflow");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => _responder(request);
    }
}