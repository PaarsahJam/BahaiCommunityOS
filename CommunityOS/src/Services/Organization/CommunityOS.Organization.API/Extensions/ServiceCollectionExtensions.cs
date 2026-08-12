using Asp.Versioning;
using CommunityOS.EventBus;
using CommunityOS.Organization.Application;
using CommunityOS.Organization.Infrastructure;
using CommunityOS.Organization.API.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace CommunityOS.Organization.API.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddOrganizationServices(
        this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOrganizationApplication()
            .AddOrganizationInfrastructure(config)
            .AddCommunityOSEventBus(config);

        services.Configure<OrganizationApiOptions>(config.GetSection(OrganizationApiOptions.SectionName));

        services
            .AddControllers()
            .AddJsonOptions(opts =>
                opts.JsonSerializerOptions.PropertyNamingPolicy =
                    System.Text.Json.JsonNamingPolicy.CamelCase);

        services.AddApiVersioning(opts =>
        {
            opts.DefaultApiVersion = new ApiVersion(1, 0);
            opts.AssumeDefaultVersionWhenUnspecified = true;
            opts.ReportApiVersions = true;
        }).AddApiExplorer(opts =>
        {
            opts.GroupNameFormat = "'v'VVV";
            opts.SubstituteApiVersionInUrl = true;
        });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddSingleton<JwtValidationKeyProvider>();
        services.AddSingleton<IConfigureNamedOptions<JwtBearerOptions>,
            ConfigureJwtBearerOptions>();

        services.AddAuthorization();

        services.AddHealthChecks();

        return services;
    }
}

/// <summary>
/// Holds the RSA key used to validate access tokens issued by the Identity
/// service. The key is shared out-of-band through configuration (PEM, XML or
/// Base64). In development a fresh ephemeral key is generated so the service
/// stays bootable; real token validation requires the configured key to match
/// the one Identity uses to sign.
/// </summary>
internal sealed class JwtValidationKeyProvider : IDisposable
{
    private readonly RSA _rsa;

    public JwtValidationKeyProvider(IConfiguration configuration)
    {
        _rsa = RSA.Create();

        var pem = configuration["Jwt:SigningPrivateKey"];
        var keyXml = configuration["Jwt:SigningKeyXml"];
        var base64 = configuration["Jwt:SigningKeyBase64"];

        if (!string.IsNullOrWhiteSpace(pem))
            _rsa.ImportFromPem(pem);
        else if (!string.IsNullOrWhiteSpace(keyXml))
            _rsa.FromXmlString(keyXml);
        else if (!string.IsNullOrWhiteSpace(base64))
            _rsa.ImportRSAPrivateKey(Convert.FromBase64String(base64), out _);
    }

    public SecurityKey SecurityKey =>
        new RsaSecurityKey(_rsa) { KeyId = "communityos-signing-key" };

    public void Dispose() => _rsa.Dispose();
}

internal sealed class ConfigureJwtBearerOptions(
    JwtValidationKeyProvider validationKeyProvider,
    IConfiguration config) : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
            return;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidateTokenReplay = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidIssuer = config["Jwt:Issuer"] ?? "CommunityOS.Identity",
            ValidAudience = config["Jwt:Audience"] ?? "CommunityOS",
            IssuerSigningKey = validationKeyProvider.SecurityKey
        };
    }

    public void Configure(JwtBearerOptions options) =>
        Configure(JwtBearerDefaults.AuthenticationScheme, options);
}
