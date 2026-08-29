using System.Net;
using System.Text;
using CommunityOS.Authorization.Infrastructure.Integration.Organization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CommunityOS.Authorization.Tests.Integration;

/// <summary>
/// Organization covers provider preserves deny-on-failure semantics (ADR-018):
/// a server error, transport failure, or malformed payload must never become an
/// authorization success. Uses the real typed client with a stub message
/// handler.
/// </summary>
public class HttpOrganizationContextProviderTests
{
    private static readonly Guid CandidateAncestor = Guid.NewGuid();
    private static readonly Guid OrgUnit = Guid.NewGuid();

    private static HttpOrganizationContextProvider Provider(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
    {
        var handler = new StubHandler(responder);
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OrganizationServiceOptions
        {
            BaseUrl = "http://organization.example",
            AccessToken = "authorization-token"
        });
        return new HttpOrganizationContextProvider(httpClient, options, NullLogger<HttpOrganizationContextProvider>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    [Fact]
    public async Task IsAncestorOrSelfAsync_OnServerError_Denies()
    {
        var provider = Provider(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var covers = await provider.IsAncestorOrSelfAsync(CandidateAncestor, OrgUnit);

        covers.Should().BeFalse();
    }

    [Fact]
    public async Task IsAncestorOrSelfAsync_OnTransportFailure_Denies()
    {
        var provider = Provider(_ => Task.FromException<HttpResponseMessage>(new HttpRequestException("unreachable")));

        var covers = await provider.IsAncestorOrSelfAsync(CandidateAncestor, OrgUnit);

        covers.Should().BeFalse();
    }

    [Fact]
    public async Task IsAncestorOrSelfAsync_OnMalformedPayload_Denies()
    {
        var provider = Provider(_ => Task.FromResult(Json(HttpStatusCode.OK, "not-json")));

        var covers = await provider.IsAncestorOrSelfAsync(CandidateAncestor, OrgUnit);

        covers.Should().BeFalse();
    }

    [Fact]
    public async Task IsAncestorOrSelfAsync_WhenCovered_Allows()
    {
        var provider = Provider(_ => Task.FromResult(Json(
            HttpStatusCode.OK,
            $$"""
            {
              "organizationUnitId": "{{OrgUnit}}",
              "candidateAncestorId": "{{CandidateAncestor}}",
              "covers": true,
              "evaluatedOn": "2026-01-01T00:00:00Z"
            }
            """)));

        var covers = await provider.IsAncestorOrSelfAsync(CandidateAncestor, OrgUnit);

        covers.Should().BeTrue();
    }

    [Fact]
    public async Task IsAncestorOrSelfAsync_WhenNotCovered_Denies()
    {
        var provider = Provider(_ => Task.FromResult(Json(
            HttpStatusCode.OK,
            $$"""
            {
              "organizationUnitId": "{{OrgUnit}}",
              "candidateAncestorId": "{{CandidateAncestor}}",
              "covers": false,
              "evaluatedOn": "2026-01-01T00:00:00Z"
            }
            """)));

        var covers = await provider.IsAncestorOrSelfAsync(CandidateAncestor, OrgUnit);

        covers.Should().BeFalse();
    }

    [Fact]
    public async Task IsAncestorOrSelfAsync_OnSuccess_SendsExpectedRequest()
    {
        HttpRequestMessage? captured = null;
        var provider = Provider(request =>
        {
            captured = request;
            return Task.FromResult(Json(
                HttpStatusCode.OK,
                $$"""
                {
                  "organizationUnitId": "{{OrgUnit}}",
                  "candidateAncestorId": "{{CandidateAncestor}}",
                  "covers": true,
                  "evaluatedOn": "2026-01-01T00:00:00Z"
                }
                """));
        });

        await provider.IsAncestorOrSelfAsync(CandidateAncestor, OrgUnit);

        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Get);
        captured.RequestUri.Should().Be(
            new Uri($"http://organization.example/api/v1/orgunits/{OrgUnit}/covers?ancestorId={CandidateAncestor}"));
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