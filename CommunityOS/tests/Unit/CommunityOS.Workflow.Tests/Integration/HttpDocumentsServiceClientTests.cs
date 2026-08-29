using System.Net;
using System.Net.Http.Headers;
using CommunityOS.Workflow.Infrastructure.Integration.Documents;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CommunityOS.Workflow.Tests.Integration;

/// <summary>
/// Documents command surface (<c>POST /documents/{id}/references</c>) preserves
/// fail-closed throw-on-failure semantics (ADR-024). Uses the real typed client
/// with a stub message handler so transport mechanics stay local and are not
/// mocked.
/// </summary>
public class HttpDocumentsServiceClientTests
{
    private static readonly Guid DocumentId = Guid.NewGuid();
    private static readonly Guid TaskId = Guid.NewGuid();

    private static HttpDocumentsServiceClient Client(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
    {
        var handler = new StubHandler(responder);
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new DocumentsServiceOptions
        {
            BaseUrl = "http://documents.example",
            AccessToken = "workflow-token"
        });
        return new HttpDocumentsServiceClient(httpClient, options, NullLogger<HttpDocumentsServiceClient>.Instance);
    }

    [Fact]
    public async Task CreateTaskReferenceAsync_OnServerError_Throws()
    {
        var client = Client(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        Func<Task> act = () => client.CreateTaskReferenceAsync(DocumentId, TaskId, "evidence");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CreateTaskReferenceAsync_OnTransportFailure_Throws()
    {
        var client = Client(_ => Task.FromException<HttpResponseMessage>(new HttpRequestException("unreachable")));

        Func<Task> act = () => client.CreateTaskReferenceAsync(DocumentId, TaskId, "evidence");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CreateTaskReferenceAsync_OnSuccess_SendsExpectedRequest()
    {
        HttpRequestMessage? captured = null;
        var client = Client(request =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        await client.CreateTaskReferenceAsync(DocumentId, TaskId, "evidence");

        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri.Should().Be(new Uri($"http://documents.example/api/v1/documents/{DocumentId}/references"));
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