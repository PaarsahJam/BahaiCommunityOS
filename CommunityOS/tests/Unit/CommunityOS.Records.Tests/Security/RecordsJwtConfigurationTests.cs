using CommunityOS.Records.API.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;

namespace CommunityOS.Records.Tests.Security;

/// <summary>
/// ADR-019 configuration regression tests for the Records API token validation
/// policy. Records follows the repository-wide JWT convention: the base
/// <c>appsettings.json</c> intentionally allows HTTP metadata discovery for
/// local development, the Production configuration forces HTTPS metadata
/// discovery, and the validation code fails closed (HTTPS required) whenever
/// the setting is absent. Production can never silently run with
/// <c>RequireHttpsMetadata = false</c>.
/// </summary>
public class RecordsJwtConfigurationTests
{
    private const string ConfigDir = "Config";

    [Fact]
    public void Base_configuration_uses_the_intentional_local_development_https_opt_out()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(ConfigDir, "appsettings.json"))
            .Build();

        // Matches the repository-wide local-dev convention (Knowledge, Documents,
        // Community). Production is guarded by the Production override below.
        config["Jwt:RequireHttpsMetadata"].Should().Be("false");
    }

    [Fact]
    public void Production_configuration_forces_https_metadata_discovery()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(ConfigDir, "appsettings.Production.json"))
            .Build();

        config["Jwt:RequireHttpsMetadata"].Should().Be("true");
        config["Jwt:MetadataAddress"].Should().StartWith("https://");
        config["Jwt:Authority"].Should().StartWith("https://");
        config["Jwt:Issuer"].Should().StartWith("https://");
    }

    [Fact]
    public void Configure_fails_closed_when_RequireHttpsMetadata_is_absent()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var options = new JwtBearerOptions();

        RecordsJwtValidation.Configure(options, config);

        options.RequireHttpsMetadata.Should().BeTrue();
    }

    [Fact]
    public void Configure_under_production_configuration_requires_https_metadata()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(ConfigDir, "appsettings.Production.json"))
            .Build();
        var options = new JwtBearerOptions();

        RecordsJwtValidation.Configure(options, config);

        options.RequireHttpsMetadata.Should().BeTrue();
        options.MetadataAddress.Should().StartWith("https://");
    }

    [Fact]
    public void Configure_under_base_configuration_allows_http_for_local_development()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(ConfigDir, "appsettings.json"))
            .Build();
        var options = new JwtBearerOptions();

        RecordsJwtValidation.Configure(options, config);

        options.RequireHttpsMetadata.Should().BeFalse();
        options.MetadataAddress.Should().StartWith("http://");
    }
}