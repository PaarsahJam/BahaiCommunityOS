using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Knowledge.API.Security;
using CommunityOS.Knowledge.Application.Permissions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace CommunityOS.Knowledge.Tests.Security;

/// <summary>
/// ADR-019 security regression tests for the Knowledge API token validation
/// policy. The API is a token consumer only: it validates RS256 tokens against
/// the Identity service's JWKS and never accepts a symmetric/HMAC secret, an
/// expired token, or a token for the wrong issuer/audience. Authorization is
/// still decided solely by the Authorization service — JWT role claims never
/// grant Knowledge business permissions.
/// </summary>
public class KnowledgeJwtSecurityTests
{
    private const string Issuer = "http://localhost:5001";
    private const string Audience = "CommunityOS";

    private static readonly RSA TrustedRsa = RSA.Create(2048);
    private static readonly RsaSecurityKey TrustedPrivateKey = new(TrustedRsa) { KeyId = "test-kid" };

    private static readonly RSA UnrelatedRsa = RSA.Create(2048);
    private static readonly RsaSecurityKey UnrelatedPrivateKey = new(UnrelatedRsa) { KeyId = "other-kid" };

    private static TokenValidationParameters ProductionPolicy(RsaSecurityKey publicKey)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience
            })
            .Build();

        var parameters = KnowledgeJwtValidation.CreateTokenValidationParameters(config);
        // In production the trusted key is published by the Identity service JWKS.
        parameters.IssuerSigningKeys = [publicKey];
        return parameters;
    }

    private static string Sign(
        RsaSecurityKey key,
        string issuer = Issuer,
        string audience = Audience,
        DateTime? notBefore = null,
        DateTime? expires = null,
        string algorithm = SecurityAlgorithms.RsaSha256,
        IEnumerable<Claim>? claims = null) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims ?? [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())],
            notBefore: notBefore ?? DateTime.UtcNow.AddMinutes(-5),
            expires: expires ?? DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(key, algorithm)));

    private static ClaimsPrincipal Validate(string token, RsaSecurityKey trustedPublicKey)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        return handler.ValidateToken(token, ProductionPolicy(trustedPublicKey), out _);
    }

    // --- JWT validation policy ---

    [Fact]
    public void Valid_RS256_token_from_trusted_key_is_accepted()
    {
        var token = Sign(TrustedPrivateKey);
        var principal = Validate(token, TrustedPrivateKey);
        principal.Should().NotBeNull();
    }

    [Fact]
    public void Token_with_invalid_signature_is_rejected()
    {
        var token = Sign(TrustedPrivateKey);
        var parts = token.Split('.');
        parts[2] = new string(parts[2].Select((c, i) => i == 0 ? (c == 'A' ? 'B' : 'A') : c).ToArray());

        var act = () => Validate(string.Join('.', parts), TrustedPrivateKey);

        act.Should().Throw<SecurityTokenValidationException>();
    }

    [Fact]
    public void Expired_token_is_rejected()
    {
        var token = Sign(TrustedPrivateKey,
            notBefore: DateTime.UtcNow.AddMinutes(-30),
            expires: DateTime.UtcNow.AddMinutes(-10));
        var act = () => Validate(token, TrustedPrivateKey);
        act.Should().Throw<SecurityTokenExpiredException>();
    }

    [Fact]
    public void Token_for_wrong_issuer_is_rejected()
    {
        var token = Sign(TrustedPrivateKey, issuer: "http://evil.example");
        var act = () => Validate(token, TrustedPrivateKey);
        act.Should().Throw<SecurityTokenInvalidIssuerException>();
    }

    [Fact]
    public void Token_for_wrong_audience_is_rejected()
    {
        var token = Sign(TrustedPrivateKey, audience: "SomeOtherService");
        var act = () => Validate(token, TrustedPrivateKey);
        act.Should().Throw<SecurityTokenInvalidAudienceException>();
    }

    [Fact]
    public void Token_signed_with_unsupported_HMAC_algorithm_is_rejected()
    {
        var hmac = new SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes("symmetric-secret-that-must-not-be-accepted-0123456789"));
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, "s")],
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(hmac, SecurityAlgorithms.HmacSha256)));

        var act = () => Validate(token, TrustedPrivateKey);

        act.Should().Throw<SecurityTokenValidationException>();
    }

    [Fact]
    public void Token_signed_by_unrelated_key_is_rejected()
    {
        var token = Sign(UnrelatedPrivateKey);
        var act = () => Validate(token, TrustedPrivateKey);
        act.Should().Throw<SecurityTokenValidationException>();
    }

    // --- JWT role claims never grant business authorization ---

    [Fact]
    public async Task Role_claims_alone_do_not_grant_business_authorization()
    {
        var token = Sign(TrustedPrivateKey, claims:
        [
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("role", "NationalAdmin")
        ]);

        // Authentication succeeds: the token is valid and carries role claims...
        var principal = Validate(token, TrustedPrivateKey);
        principal.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");

        // ...but the Knowledge API never interprets those claims. The guard
        // still asks the Authorization service, which denies without a grant.
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var evaluator = Substitute.For<IAuthorizationEvaluator>();
        evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationDecision.Deny("dec", AuthorizationDecisionReason.NoPermission, DateTime.UtcNow));
        var guard = new AuthorizationGuard(evaluator);

        var act = () => guard.RequireAsync(Guid.Parse(sub!), KnowledgePermissions.QuestionRead,
            new AuthorizationContext(ResourceType: "question"), CancellationToken.None);

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
    }
}