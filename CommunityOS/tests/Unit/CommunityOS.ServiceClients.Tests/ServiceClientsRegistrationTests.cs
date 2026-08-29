using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CommunityOS.ServiceClients.Tests;

public class ServiceClientsRegistrationTests
{
    private sealed class TestOptions : ServiceClientOptions
    {
        public const string SectionName = "TestService";

        public TestOptions()
        {
            ClientId = "test-client";
        }
    }

    private sealed class TestHttpClient
    {
        public TestHttpClient(HttpClient client, IOptions<TestOptions> options)
        {
            Client = client;
            Options = options.Value;
        }

        public HttpClient Client { get; }

        public TestOptions Options { get; }
    }

    private static IConfiguration Config(string? baseUrl = null)
    {
        var data = new Dictionary<string, string?>
        {
            ["TestService:AccessToken"] = "service-token"
        };
        if (baseUrl is not null)
            data["TestService:BaseUrl"] = baseUrl;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
    }

    [Fact]
    public void AddServiceHttpClient_MissingBaseUrl_FailsClosedOnResolution()
    {
        var services = new ServiceCollection();
        services.AddServiceHttpClient<TestHttpClient, TestOptions>(Config(), TestOptions.SectionName);

        using var sp = services.BuildServiceProvider();
        Action act = () => sp.GetRequiredService<TestHttpClient>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TestService:BaseUrl is not configured*");
    }

    [Fact]
    public void AddServiceHttpClient_SetsBaseAddressFromOptions()
    {
        var services = new ServiceCollection();
        services.AddServiceHttpClient<TestHttpClient, TestOptions>(Config("https://target.example"), TestOptions.SectionName);

        using var sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<TestHttpClient>();

        client.Client.BaseAddress.Should().Be(new Uri("https://target.example"));
    }

    [Fact]
    public void AddServiceHttpClient_BindsOptionsFromSection()
    {
        var services = new ServiceCollection();
        services.AddServiceHttpClient<TestHttpClient, TestOptions>(Config("https://target.example"), TestOptions.SectionName);

        using var sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<TestHttpClient>();

        client.Options.BaseUrl.Should().Be("https://target.example");
        client.Options.AccessToken.Should().Be("service-token");
        client.Options.ClientId.Should().Be("test-client");
    }

    [Fact]
    public void AddServiceHttpClient_ValidateCallback_FailsClosedOnMissingBaseUrl()
    {
        var services = new ServiceCollection();
        services.AddServiceHttpClient<TestHttpClient, TestOptions>(
            Config(),
            TestOptions.SectionName,
            o => o.Validate(x => !string.IsNullOrWhiteSpace(x.BaseUrl), "TestService:BaseUrl is required."));

        using var sp = services.BuildServiceProvider();
        Action act = () => sp.GetRequiredService<TestHttpClient>();

        act.Should().Throw<OptionsValidationException>();
    }
}