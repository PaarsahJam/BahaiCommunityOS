using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Xunit;

namespace CommunityOS.Host.ApiGateway.Tests;

public class GatewayForwardingTests
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        using var fixture = new GatewayTestFixture();
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_ForwardsToCommunity_PreservingPathVersionMethodAndQuery()
    {
        using var fixture = new GatewayTestFixture();
        var client = fixture.CreateClient();
        fixture.Handler.Responder = _ => Json("application/json", "[]");

        var response = await client.GetAsync("/api/v1/persons?include=address&page=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var downstream = fixture.Handler.CapturedRequests.Should().ContainSingle().Subject;
        downstream.Method.Should().Be(HttpMethod.Get);
        downstream.RequestUri!.AbsolutePath.Should().Be("/api/v1/persons");
        downstream.RequestUri.GetLeftPart(UriPartial.Authority).Should().Be("http://localhost:5004");
        downstream.RequestUri.Query.Should().Contain("include=address").And.Contain("page=2");
    }

    [Fact]
    public async Task Post_ForwardsBodyAndMethod_PreservingApiVersion()
    {
        using var fixture = new GatewayTestFixture();
        var client = fixture.CreateClient();
        fixture.Handler.Responder = _ => Json("application/json", "{\"id\":1}");

        const string payload = "{\"name\":\"Ada\"}";
        var response = await client.PostAsync(
            "/api/v2/persons",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var downstream = fixture.Handler.CapturedRequests.Should().ContainSingle().Subject;
        downstream.Method.Should().Be(HttpMethod.Post);
        downstream.RequestUri!.AbsolutePath.Should().Be("/api/v2/persons");
        var body = await downstream.Content!.ReadAsStringAsync();
        body.Should().Be(payload);
        downstream.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task OrganizationPrefix_ForwardsToOrganization()
    {
        using var fixture = new GatewayTestFixture();
        var client = fixture.CreateClient();
        fixture.Handler.Responder = _ => Json("application/json", "{}");

        var response = await client.GetAsync("/api/v1/organizations/abc");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var downstream = fixture.Handler.CapturedRequests.Should().ContainSingle().Subject;
        downstream.RequestUri!.GetLeftPart(UriPartial.Authority).Should().Be("http://localhost:5003");
        downstream.RequestUri.AbsolutePath.Should().Be("/api/v1/organizations/abc");
    }

    [Fact]
    public async Task AnonymousIdentityRoute_ForwardsWithoutAuthentication()
    {
        using var fixture = new GatewayTestFixture();
        var client = fixture.CreateClient();
        fixture.Handler.Responder = _ => Json("application/json", "{}");

        var response = await client.GetAsync("/api/v1/auth/login");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var downstream = fixture.Handler.CapturedRequests.Should().ContainSingle().Subject;
        downstream.RequestUri!.GetLeftPart(UriPartial.Authority).Should().Be("http://localhost:5001");
    }

    [Fact]
    public async Task InternalOrganizationCoversRoute_IsNotExposed()
    {
        using var fixture = new GatewayTestFixture();
        var client = fixture.CreateClient();
        fixture.Handler.Responder = _ => Json("application/json", "{}");

        var response = await client.GetAsync(
            "/api/v1/orgunits/00000000-0000-0000-0000-000000000000/covers");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        fixture.Handler.CapturedRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownPublicRoute_ReturnsNotFound_WithoutForwarding()
    {
        using var fixture = new GatewayTestFixture();
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/v1/not-a-route");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        fixture.Handler.CapturedRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task DownstreamStatusAndProblemDetails_PassThroughUnchanged()
    {
        using var fixture = new GatewayTestFixture();
        var client = fixture.CreateClient();
        const string problem = "{\"title\":\"Bad\",\"status\":400,\"errors\":{\"name\":[\"required\"]}}";
        fixture.Handler.Responder = _ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(problem, Encoding.UTF8, "application/problem+json")
            };

        var response = await client.GetAsync("/api/v1/meetings");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadAsStringAsync()).Should().Be(problem);
    }

    private static HttpResponseMessage Json(string contentType, string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType)
        };
}
