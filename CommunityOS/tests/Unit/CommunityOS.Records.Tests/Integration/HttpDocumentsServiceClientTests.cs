using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CommunityOS.Records.Application.Abstractions;
using CommunityOS.Records.Infrastructure.Integration.Documents;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CommunityOS.Records.Tests.Integration;

/// <summary>
/// Documents command/read surface (<c>GET /documents/{id}</c>,
/// <c>POST /documents/{id}/classify</c>, <c>POST /documents/{id}/references</c>)
/// preserves fail-closed throw-on-failure semantics (ADR-023). Uses the real
/// typed client with a stub message handler.
/// </summary>
public class HttpDocumentsServiceClientTests
{
    private static readonly Guid DocumentId = Guid.NewGuid();

    private static HttpDocumentsServiceClient Client(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
    {
        var handler = new StubHandler(responder);
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new DocumentsServiceOptions
        {
            BaseUrl = "http://documents.example",
            AccessToken = "records-token"
        });
        return new HttpDocumentsServiceClient(httpClient, options, NullLogger<HttpDocumentsServiceClient>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    [Fact]
    public async Task ClassifyAsync_OnServerError_Throws()
    {
        var client = Client(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var state = new DocumentClassificationState("Records", IsSensitive: true, "Retain-10", null, null);
        Func<Task> act = () => client.ClassifyAsync(DocumentId, state);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ClassifyAsync_OnTransportFailure_Throws()
    {
        var client = Client(_ => Task.FromException<HttpResponseMessage>(new HttpRequestException("unreachable")));

        var state = new DocumentClassificationState("Records", IsSensitive: true, "Retain-10", null, null);
        Func<Task> act = () => client.ClassifyAsync(DocumentId, state);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CreateReferenceAsync_OnServerError_Throws()
    {
        var client = Client(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        Func<Task> act = () => client.CreateReferenceAsync(DocumentId, "records.evidence", Guid.NewGuid(), "evidence");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetClassificationAsync_OnSuccess_ReturnsClassificationState()
    {
        var json =
            $$"""
            {
              "id": "{{DocumentId}}",
              "title": "File",
              "status": "Active",
              "classification": {
                "classificationCode": "Records",
                "isSensitive": true,
                "retentionCategory": "Retain-10",
                "legalHoldReference": "LH-1",
                "administrativeHoldReference": null
              }
            }
            """;
        var client = Client(_ => Task.FromResult(Json(HttpStatusCode.OK, json)));

        var state = await client.GetClassificationAsync(DocumentId);

        state.ClassificationCode.Should().Be("Records");
        state.IsSensitive.Should().BeTrue();
        state.RetentionCategory.Should().Be("Retain-10");
        state.LegalHoldReference.Should().Be("LH-1");
        state.AdministrativeHoldReference.Should().BeNull();
    }

    [Fact]
    public async Task ClassifyAsync_OnSuccess_SendsExpectedRequest()
    {
        HttpRequestMessage? captured = null;
        var client = Client(request =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var state = new DocumentClassificationState("Records", IsSensitive: true, "Retain-10", null, null);
        await client.ClassifyAsync(DocumentId, state);

        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri.Should().Be(new Uri($"http://documents.example/api/v1/documents/{DocumentId}/classify"));
        captured.Headers.Authorization.Should().Be(
            new AuthenticationHeaderValue("Bearer", "records-token"));
        captured.Headers.TryGetValues("X-Client-Id", out var values).Should().BeTrue();
        values.Should().ContainSingle().Which.Should().Be("communityos-records");
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
