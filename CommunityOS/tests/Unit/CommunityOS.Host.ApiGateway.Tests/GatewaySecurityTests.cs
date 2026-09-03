using System.Net;
using System.Text;
using FluentAssertions;
using Xunit;

namespace CommunityOS.Host.ApiGateway.Tests;

/// <summary>
/// The critical Gateway security boundary (ADR-035): the Gateway forwards the
/// caller's access token transparently and must not replace, mint, remove, or
/// interpret it, and must never present its own service credential.
/// </summary>
public class GatewaySecurityTests
{
    private const string OriginalToken = "ORIGINAL_ACCESS_TOKEN";

    [Fact]
    public async Task AuthorizationHeader_IsForwardedVerbatim()
    {
        using var fixture = new GatewayTestFixture();
        var client = fixture.CreateClient();
        fixture.Handler.Responder = _ => Json("{}");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/persons");
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {OriginalToken}");
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var downstream = fixture.Handler.CapturedRequests.Should().ContainSingle().Subject;

        var scheme = downstream.Headers.Authorization?.Scheme;
        var parameter = downstream.Headers.Authorization?.Parameter;
        scheme.Should().Be("Bearer");
        parameter.Should().Be(OriginalToken);
    }

    [Fact]
    public async Task DownstreamRequest_DoesNotAddGatewayOrServiceCredential()
    {
        using var fixture = new GatewayTestFixture();
        var client = fixture.CreateClient();
        fixture.Handler.Responder = _ => Json("{}");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/account/security");
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {OriginalToken}");
        await client.SendAsync(request);

        var downstream = fixture.Handler.CapturedRequests.Should().ContainSingle().Subject;
        downstream.Headers.Authorization!.Parameter.Should().Be(OriginalToken);
    }

    [Fact]
    public async Task ForwardedRequest_DoesNotCarryXClientIdInternalIdentity()
    {
        using var fixture = new GatewayTestFixture();
        var client = fixture.CreateClient();
        fixture.Handler.Responder = _ => Json("{}");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/persons/1");
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {OriginalToken}");
        await client.SendAsync(request);

        var downstream = fixture.Handler.CapturedRequests.Should().ContainSingle().Subject;
        downstream.Headers.Contains("X-Client-Id").Should().BeFalse();
    }

    [Fact]
    public async Task Forwarding_NeverCallsAuthorizationCheckEndpoint()
    {
        using var fixture = new GatewayTestFixture();
        var client = fixture.CreateClient();
        fixture.Handler.Responder = _ => Json("{}");

        await client.GetAsync("/api/v1/meetings");
        await client.GetAsync("/api/v1/persons/1");
        await client.GetAsync("/api/v1/authz/");

        var downstreamRequests = fixture.Handler.CapturedRequests;
        downstreamRequests.Should().NotBeEmpty();
        foreach (var request in downstreamRequests)
            request.RequestUri!.AbsolutePath.Should().NotContain("authz/check");
    }

    [Fact]
    public async Task InvalidJwt_IsStillForwarded_NoGatewayAuthorizationDecision()
    {
        using var fixture = new GatewayTestFixture();
        var client = fixture.CreateClient();
        fixture.Handler.Responder = _ => Json("{}");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/persons/9");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer NOT_A_VALID_JWT_SIGNATURE");
        var response = await client.SendAsync(request);

        // The Gateway does not validate the token; it forwards transparently and
        // leaves authentication to the downstream service.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var downstream = fixture.Handler.CapturedRequests.Should().ContainSingle().Subject;
        downstream.Headers.Authorization!.Parameter.Should().Be("NOT_A_VALID_JWT_SIGNATURE");
    }

    [Fact]
    public async Task CallerSuppliedXClientId_IsNotForwarded()
    {
        using var fixture = new GatewayTestFixture();
        var client = fixture.CreateClient();
        fixture.Handler.Responder = _ => Json("{}");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/persons/1");
        request.Headers.TryAddWithoutValidation("X-Client-Id", "external-spoofed-id");
        await client.SendAsync(request);

        var downstream = fixture.Handler.CapturedRequests.Should().ContainSingle().Subject;
        downstream.Headers.Contains("X-Client-Id").Should().BeFalse();
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
}
