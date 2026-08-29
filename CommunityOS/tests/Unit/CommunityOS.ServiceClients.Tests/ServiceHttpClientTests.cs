using FluentAssertions;
using Xunit;

namespace CommunityOS.ServiceClients.Tests;

public class ServiceHttpClientTests
{
    private sealed class TestOptions : ServiceClientOptions
    {
        public TestOptions()
        {
            ClientId = "test-client";
        }
    }

    private static TestOptions Options(string baseUrl = "https://target.example", string token = "tok-123")
        => new()
        {
            BaseUrl = baseUrl,
            AccessToken = token
        };

    [Fact]
    public void CreateRequest_TrailingSlashBaseUrl_ComposesUri()
    {
        using var request = ServiceHttpClient.CreateRequest(
            Options("https://target.example/"), HttpMethod.Get, "/api/v1/resources");

        request.RequestUri.Should().Be(new Uri("https://target.example/api/v1/resources"));
    }

    [Fact]
    public void CreateRequest_NoTrailingSlashBaseUrl_ComposesUri()
    {
        using var request = ServiceHttpClient.CreateRequest(
            Options("https://target.example"), HttpMethod.Get, "/api/v1/resources");

        request.RequestUri.Should().Be(new Uri("https://target.example/api/v1/resources"));
    }

    [Fact]
    public void CreateRequest_SetsBearerAuthorizationHeader()
    {
        using var request = ServiceHttpClient.CreateRequest(
            Options(token: "secret-token"), HttpMethod.Get, "/api/v1/resources");

        request.Headers.Authorization.Should().NotBeNull();
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be("secret-token");
    }

    [Fact]
    public void CreateRequest_SetsXClientIdHeader()
    {
        using var request = ServiceHttpClient.CreateRequest(Options(), HttpMethod.Get, "/api/v1/resources");

        request.Headers.TryGetValues("X-Client-Id", out var values).Should().BeTrue();
        values.Should().ContainSingle().Which.Should().Be("test-client");
    }

    [Fact]
    public async Task CreateRequest_WithBody_SerializesAsWebCamelCaseJson()
    {
        using var request = ServiceHttpClient.CreateRequest(
            Options(), HttpMethod.Post, "/api/v1/resources",
            new { HelloWorld = "value", NestedProp = 42 });

        request.Content.Should().NotBeNull();
        var json = await request.Content!.ReadAsStringAsync();
        json.Should().Contain("\"helloWorld\"");
        json.Should().Contain("\"nestedProp\"");
        json.Should().NotContain("HelloWorld");
    }

    [Fact]
    public void CreateRequest_WithoutBody_HasNoContent()
    {
        using var request = ServiceHttpClient.CreateRequest(Options(), HttpMethod.Get, "/api/v1/resources");

        request.Content.Should().BeNull();
    }
}