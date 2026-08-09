using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace CommunityOS.Identity.Infrastructure.Security;

/// <summary>
/// Builds the OpenID Connect discovery document advertised at
/// <c>/.well-known/openid-configuration</c>. The issuer is read from
/// configuration; endpoint URLs are derived from it so the document stays
/// correct behind a single public origin.
/// </summary>
public sealed class OidcDiscoveryDocument(IConfiguration configuration)
{
    private readonly string _issuer =
        configuration["Oidc:Issuer"] ?? "http://localhost:5001";

    public string BuildJson()
    {
        var document = new
        {
            issuer = _issuer,
            authorization_endpoint = $"{_issuer}/api/v1/connect/authorize",
            token_endpoint = $"{_issuer}/api/v1/connect/token",
            jwks_uri = $"{_issuer}/api/v1/.well-known/jwks",
            response_types_supported = new[] { "code" },
            grant_types_supported = new[] { "authorization_code", "refresh_token" },
            subject_types_supported = new[] { "public" },
            id_token_signing_alg_values_supported = new[] { "RS256" },
            code_challenge_methods_supported = new[] { "S256", "plain" },
            scopes_supported = new[] { "openid", "profile", "email", "offline_access" },
            token_endpoint_auth_methods_supported = new[] { "none" },
            claims_supported = new[]
            {
                "sub", "email", "email_verified", "iss", "aud", "exp", "iat", "nonce"
            }
        };

        return JsonSerializer.Serialize(document);
    }
}
