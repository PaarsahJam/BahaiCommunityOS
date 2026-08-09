using CommunityOS.Identity.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace CommunityOS.Identity.Infrastructure.Security;

/// <summary>
/// Issues short-lived RS256-signed access tokens (JWT) and opaque refresh
/// tokens. Access tokens carry only the user identity and issued-at/lifetime
/// claims. Tokens are never logged by this service.
/// </summary>
public sealed class JwtTokenService(
    RsaSigningKeyProvider signingKeyProvider,
    IConfiguration configuration) : ITokenService
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    public string GenerateAccessToken(Guid userAccountId, string email)
    {
        var audience = configuration["Jwt:Audience"] ?? "CommunityOS";
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userAccountId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        return WriteSignedToken(claims, audience, AccessTokenLifetime);
    }

    public string GenerateIdToken(Guid userAccountId, string email, bool emailVerified, string audience, string? nonce)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userAccountId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new("email_verified", emailVerified ? "true" : "false", ClaimValueTypes.Boolean),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        if (!string.IsNullOrWhiteSpace(nonce))
            claims.Add(new Claim("nonce", nonce));

        return WriteSignedToken(claims, audience, AccessTokenLifetime);
    }

    private string WriteSignedToken(IEnumerable<Claim> claims, string audience, TimeSpan lifetime)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "CommunityOS.Identity";
        var now = DateTime.UtcNow;

        var credentials = new SigningCredentials(
            signingKeyProvider.SecurityKey, signingKeyProvider.SigningAlgorithm);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(lifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }
}
