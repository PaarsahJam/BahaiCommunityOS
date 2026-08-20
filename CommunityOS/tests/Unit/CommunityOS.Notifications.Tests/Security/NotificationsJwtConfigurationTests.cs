using CommunityOS.Notifications.API.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CommunityOS.Notifications.Tests.Security;

/// <summary>
/// ADR-019 configuration regression tests for the Notifications API token
/// validation policy. Notifications follows the repository-wide JWT convention:
/// the base <c>appsettings.json</c> intentionally allows HTTP metadata discovery
/// for local development, the Production configuration forces HTTPS metadata
/// discovery, and the validation code fails closed (HTTPS required) whenever the
/// setting is absent. Production can never silently run with
/// <c>RequireHttpsMetadata = false</c>.
/// </summary>
public class NotificationsJwtConfigurationTests
{
    private const string ConfigDir = "Config";

    [Fact]
    public void Base_configuration_uses_the_intentional_local_development_https_opt_out()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(ConfigDir, "appsettings.json"))
            .Build();

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

        NotificationsJwtValidation.Configure(options, config);

        options.RequireHttpsMetadata.Should().BeTrue();
    }

    [Fact]
    public void Configure_under_production_configuration_requires_https_metadata()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(ConfigDir, "appsettings.Production.json"))
            .Build();
        var options = new JwtBearerOptions();

        NotificationsJwtValidation.Configure(options, config);

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

        NotificationsJwtValidation.Configure(options, config);

        options.RequireHttpsMetadata.Should().BeFalse();
        options.MetadataAddress.Should().StartWith("http://");
    }

    [Fact]
    public void Token_validation_policy_is_rs256_only_and_fail_closed()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "CommunityOS"
            })
            .Build();

        var parameters = NotificationsJwtValidation.CreateTokenValidationParameters(config);

        parameters.ValidateIssuer.Should().BeTrue();
        parameters.ValidateAudience.Should().BeTrue();
        parameters.ValidateLifetime.Should().BeTrue();
        parameters.ValidateIssuerSigningKey.Should().BeTrue();
        parameters.ValidAlgorithms.Should().Contain(SecurityAlgorithms.RsaSha256);
        parameters.ValidAlgorithms.Should().NotContain(SecurityAlgorithms.HmacSha256);
    }
}