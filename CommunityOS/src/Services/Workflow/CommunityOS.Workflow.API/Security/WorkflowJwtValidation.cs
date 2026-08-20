using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace CommunityOS.Workflow.API.Security;

/// <summary>
/// RS256/JWKS validation for access tokens issued by the Identity service
/// (ADR-019). Workflow is a token consumer only: it never holds the Identity
/// private signing key and never shares a symmetric secret. Public signing
/// keys are discovered from the Identity service's JWKS endpoint advertised by
/// its OpenID Connect discovery document.
/// </summary>
public static class WorkflowJwtValidation
{
    /// <summary>OpenID Connect discovery path served by the Identity service.</summary>
    public const string DefaultDiscoveryPath = "/api/v1/.well-known/openid-configuration";

    public static void Configure(JwtBearerOptions options, IConfiguration config)
    {
        var metadataAddress = config["Jwt:MetadataAddress"];
        if (string.IsNullOrWhiteSpace(metadataAddress))
        {
            var authority = config["Jwt:Authority"]?.TrimEnd('/');
            metadataAddress = string.IsNullOrWhiteSpace(authority)
                ? null
                : authority + DefaultDiscoveryPath;
        }

        if (!string.IsNullOrWhiteSpace(metadataAddress))
            options.MetadataAddress = metadataAddress;

        options.RequireHttpsMetadata =
            config.GetValue("Jwt:RequireHttpsMetadata", true);

        options.TokenValidationParameters = CreateTokenValidationParameters(config);
    }

    /// <summary>
    /// Token validation policy: RS256 only, issuer/audience/lifetime/signature
    /// all validated, fail-closed. No HMAC algorithm is accepted.
    /// </summary>
    public static TokenValidationParameters CreateTokenValidationParameters(IConfiguration config) => new()
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ClockSkew                = TimeSpan.FromSeconds(30),
        ValidIssuer              = config["Jwt:Issuer"],
        ValidAudience            = config["Jwt:Audience"],
        ValidAlgorithms          = [SecurityAlgorithms.RsaSha256]
    };
}