using CommunityOS.Host.ApiGateway.Extensions;
using CommunityOS.Host.ApiGateway.Tests.TestDoubles;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CommunityOS.Host.ApiGateway.Tests;

/// <summary>
/// Hosts the real Gateway bootstrap (the same <see cref="Extensions"/> services
/// and pipeline the Program uses) in-process via <see cref="TestServer"/>, with
/// no Docker requirement. The outbound <see cref="IHttpClientFactory"/> is
/// replaced by a recording stub so tests observe exactly what the Gateway
/// forwards downstream (ADR-035).
/// </summary>
public sealed class GatewayTestFixture : IDisposable
{
    private readonly WebApplication _app;
    private readonly HttpClient _client;

    public RecordingDownstreamHandler Handler { get; } = new();

    public GatewayTestFixture()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Services:Identity:BaseUrl"] = "http://localhost:5001",
            ["Services:Authorization:BaseUrl"] = "http://localhost:5007",
            ["Services:Organization:BaseUrl"] = "http://localhost:5003",
            ["Services:Community:BaseUrl"] = "http://localhost:5004",
            ["Services:Notifications:BaseUrl"] = "http://localhost:5008"
        });

        builder.Services.AddGatewayServices(builder.Configuration);
        builder.Services.RemoveAll<IHttpClientFactory>();
        builder.Services.AddSingleton<IHttpClientFactory>(new StubHttpClientFactory(Handler));

        _app = builder.Build();
        _app.MapGatewayPipeline();
        _app.StartAsync().GetAwaiter().GetResult();

        _client = _app.GetTestClient();
    }

    public HttpClient CreateClient() => _client;

    public void Dispose()
    {
        _client.Dispose();
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
