using CommunityOS.Identity.API.Extensions;
using FluentAssertions;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CommunityOS.Identity.Tests.API;

public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetSessionFamilyId_ReadsSignedSidClaim()
    {
        var familyId = Guid.NewGuid();
        var principal = PrincipalWith(
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Sid, familyId.ToString()));

        principal.GetSessionFamilyId().Should().Be(familyId);
    }

    [Fact]
    public void GetSessionFamilyId_WithNoSidClaim_ReturnsNull()
    {
        var principal = PrincipalWith(
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()));

        principal.GetSessionFamilyId().Should().BeNull();
    }

    [Fact]
    public void GetSessionFamilyId_WithMalformedSid_ReturnsNull_AndDoesNotThrow()
    {
        var principal = PrincipalWith(
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Sid, "not-a-guid"));

        var act = () => principal.GetSessionFamilyId();

        act.Should().NotThrow();
        principal.GetSessionFamilyId().Should().BeNull();
    }

    [Fact]
    public void GetUserAccountId_StillReadsSubjectOnly_AndIsUnaffectedBySid()
    {
        var accountId = Guid.NewGuid();
        var principal = PrincipalWith(
            new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()),
            new Claim(JwtRegisteredClaimNames.Sid, Guid.NewGuid().ToString()));

        principal.GetUserAccountId().Should().Be(accountId);
    }

    [Fact]
    public void GetUserAccountId_WithNoSubject_ThrowsUnauthorized()
    {
        var principal = PrincipalWith(new Claim(JwtRegisteredClaimNames.Sid, Guid.NewGuid().ToString()));

        var act = () => principal.GetUserAccountId();

        act.Should().Throw<UnauthorizedAccessException>();
    }

    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));
}