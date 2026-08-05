using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CommunityOS.Identity.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CommunityOS.Identity.Infrastructure.ExternalServices;

internal sealed class JwtTokenService(IConfiguration config) : ITokenService
{
    private readonly string _secret = config["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
    private readonly string _issuer  = config["Jwt:Issuer"]  ?? "CommunityOS";
    private readonly string _audience = config["Jwt:Audience"] ?? "CommunityOS";
    private readonly int _expiryMinutes = int.Parse(config["Jwt:ExpiryMinutes"] ?? "60");

    public string GenerateAccessToken(Guid memberId, string email, IEnumerable<string> roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, memberId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public Guid? ValidateRefreshToken(string refreshToken) =>
        null; // Stateful refresh token validation handled by repository lookup
}
