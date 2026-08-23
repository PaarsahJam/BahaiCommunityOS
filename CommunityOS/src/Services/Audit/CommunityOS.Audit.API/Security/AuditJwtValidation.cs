using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace CommunityOS.Audit.API.Security;

public static class AuditJwtValidation
{
    public const string DefaultDiscoveryPath = "/api/v1/.well-known/openid-configuration";

    public static void Configure(JwtBearerOptions options, IConfiguration config)
    {
        var metadata = config["Jwt:MetadataAddress"];
        if (string.IsNullOrWhiteSpace(metadata) && !string.IsNullOrWhiteSpace(config["Jwt:Authority"]))
        {
            metadata = config["Jwt:Authority"]!.TrimEnd('/') + DefaultDiscoveryPath;
        }

        if (!string.IsNullOrWhiteSpace(metadata))
        {
            options.MetadataAddress = metadata;
        }

        options.RequireHttpsMetadata = config.GetValue("Jwt:RequireHttpsMetadata", true);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidAudience = config["Jwt:Audience"],
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (context.Principal?.FindFirst("sub") is null &&
                    context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) is null)
                {
                    context.Fail("Missing subject.");
                }

                return Task.CompletedTask;
            }
        };
    }
}
