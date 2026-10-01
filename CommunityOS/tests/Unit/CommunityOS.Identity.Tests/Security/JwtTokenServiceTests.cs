using CommunityOS.Identity.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace CommunityOS.Identity.Tests.Security;

public sealed class RsaSigningKeyProviderTests
{
    [Fact]
    public void Provider_GeneratesDevelopmentKey_WhenNoKeyConfigured()
    {
        var sut = new RsaSigningKeyProvider(EmptyConfig());

        var jwks = sut.GenerateJwksJson();

        jwks.Should().NotBeNullOrWhiteSpace();
        using var doc = JsonDocument.Parse(jwks);
        doc.RootElement.GetProperty("keys").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void Jwks_ContainsOnlyPublicKeyMaterial()
    {
        var sut = new RsaSigningKeyProvider(EmptyConfig());

        var jwks = sut.GenerateJwksJson();

        jwks.Should().NotContain("BEGIN PRIVATE KEY");
        jwks.Should().Contain("\"kty\":\"RSA\"");
        jwks.Should().Contain("\"alg\":\"RS256\"");
    }

    [Fact]
    public void GeneratedToken_CanBeValidatedWithPublicJwk()
    {
        var provider = new RsaSigningKeyProvider(EmptyConfig());
        var sut = new JwtTokenService(provider, EmptyConfig());

        var token = sut.GenerateAccessToken(Guid.NewGuid(), Guid.NewGuid(), "user@example.com", 0);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "CommunityOS.Identity",
            ValidAudience = "CommunityOS",
            IssuerSigningKey = provider.SecurityKey
        }, out _);

        principal.FindFirstValue(JwtRegisteredClaimNames.Email).Should().Be("user@example.com");
    }

    [Fact]
    public void JwtService_ProducesValidJwtStructure()
    {
        var provider = new RsaSigningKeyProvider(EmptyConfig());
        var sut = new JwtTokenService(provider, EmptyConfig());
        var id = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        var token = sut.GenerateAccessToken(id, familyId, "user@example.com", 0);

        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();
        var jwt = handler.ReadJwtToken(token);
        jwt.Subject.Should().Be(id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
    }

    [Fact]
    public void AccessToken_SidCarriesIssuingTokenFamilyId_DistinctFromJti()
    {
        var provider = new RsaSigningKeyProvider(EmptyConfig());
        var sut = new JwtTokenService(provider, EmptyConfig());
        var familyId = Guid.NewGuid();

        var token = sut.GenerateAccessToken(Guid.NewGuid(), familyId, "user@example.com", 0);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var sid = jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sid).Value;
        var jti = jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        sid.Should().Be(familyId.ToString());
        // The token instance id (jti) is unrelated to the logical session id.
        jti.Should().NotBe(sid);
    }

    [Fact]
    public void RefreshToken_IsOpaqueAndUnique()
    {
        var provider = new RsaSigningKeyProvider(EmptyConfig());
        var sut = new JwtTokenService(provider, EmptyConfig());

        var a = sut.GenerateRefreshToken();
        var b = sut.GenerateRefreshToken();

        a.Should().NotBe(b);
        a.Should().NotContain(".");
    }

    [Fact]
    public void AccessToken_EpochZero_ProducesNumericSreClaimEqualToZero()
    {
        var provider = new RsaSigningKeyProvider(EmptyConfig());
        var sut = new JwtTokenService(provider, EmptyConfig());

        var token = sut.GenerateAccessToken(Guid.NewGuid(), Guid.NewGuid(), "user@example.com", 0);

        NumericSre(token).Should().Be(0);
    }

    [Fact]
    public void AccessToken_EpochOne_ProducesNumericSreClaimEqualToOne()
    {
        var provider = new RsaSigningKeyProvider(EmptyConfig());
        var sut = new JwtTokenService(provider, EmptyConfig());

        var token = sut.GenerateAccessToken(Guid.NewGuid(), Guid.NewGuid(), "user@example.com", 1);

        NumericSre(token).Should().Be(1);
    }

    [Fact]
    public void AccessToken_HigherPersistedEpoch_IsReflectedInNewlyIssuedToken()
    {
        var provider = new RsaSigningKeyProvider(EmptyConfig());
        var sut = new JwtTokenService(provider, EmptyConfig());

        var token = sut.GenerateAccessToken(Guid.NewGuid(), Guid.NewGuid(), "user@example.com", 42);

        NumericSre(token).Should().Be(42);
    }

    [Fact]
    public void AccessToken_SreClaim_IsSerializedAsJsonNumber_NotArbitraryString()
    {
        var provider = new RsaSigningKeyProvider(EmptyConfig());
        var sut = new JwtTokenService(provider, EmptyConfig());

        var token = sut.GenerateAccessToken(Guid.NewGuid(), Guid.NewGuid(), "user@example.com", 7);

        // The ADR-036 contract requires a JSON numeric claim, never a quoted
        // string. The raw wire payload must carry "sre":7, not "sre":"7".
        var payload = DecodePayload(token);
        using var doc = JsonDocument.Parse(payload);
        var sre = doc.RootElement.GetProperty("sre");

        sre.ValueKind.Should().Be(JsonValueKind.Number);
        sre.GetInt64().Should().Be(7);
        payload.Should().Contain("\"sre\":7");
        payload.Should().NotContain("\"sre\":\"");
    }

    private static long NumericSre(string token)
    {
        var payload = DecodePayload(token);
        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.GetProperty("sre").GetInt64();
    }

    private static string DecodePayload(string token)
    {
        var parts = token.Split('.');
        parts.Should().HaveCount(3);
        return Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1]));
    }

    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection().Build();
}
