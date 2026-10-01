using Asp.Versioning;
using CommunityOS.EventBus;
using CommunityOS.Identity.API.Security;
using CommunityOS.Identity.Application;
using CommunityOS.Identity.Infrastructure;
using CommunityOS.Identity.Infrastructure.Persistence;
using CommunityOS.Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CommunityOS.Identity.API.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddIdentityServices(
        this IServiceCollection services, IConfiguration config)
    {
        services
            .AddIdentityApplication()
            .AddIdentityInfrastructure(config)
            .AddCommunityOSEventBusWithOutbox<IdentityDbContext>(config);

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

        services.AddSingleton<IConfigureNamedOptions<JwtBearerOptions>,
            ConfigureJwtBearerOptions>();

        services.AddAuthorization();

        return services;
    }
}

internal sealed class ConfigureJwtBearerOptions(
    RsaSigningKeyProvider signingKeyProvider,
    IConfiguration config) : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
            return;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidateTokenReplay      = true,
            ClockSkew                = TimeSpan.FromSeconds(30),
            ValidIssuer              = config["Jwt:Issuer"] ?? "CommunityOS.Identity",
            ValidAudience            = config["Jwt:Audience"] ?? "CommunityOS",
            IssuerSigningKey         = signingKeyProvider.SecurityKey
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = AccessTokenEpochValidationEvents.OnTokenValidatedAsync
        };
    }

    public void Configure(JwtBearerOptions options) =>
        Configure(JwtBearerDefaults.AuthenticationScheme, options);
}
