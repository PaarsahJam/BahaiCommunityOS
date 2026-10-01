using CommunityOS.Identity.Infrastructure.Security;
using FluentAssertions;
using System.Security.Claims;

namespace CommunityOS.Identity.Tests.Security;

/// <summary>
/// ADR-036 D1/Q1: claim parsing and validity-rule behavior of
/// <see cref="SessionRevocationEpochValidator"/> — absent defaults to epoch 0,
/// numeric parsing, malformed/non-numeric fails closed, negative values cannot
/// bypass revocation, and the compare rule (equal accepted, newer accepted,
/// older rejected) follows the agreed ADR-036 model.
/// </summary>
public sealed class SessionRevocationEpochValidatorTests
{
    private const string Integer32 = "http://www.w3.org/2001/XMLSchema#integer32";
    private const string Integer64 = "http://www.w3.org/2001/XMLSchema#integer64";
    private const string Double = "http://www.w3.org/2001/XMLSchema#double";
    private const string Boolean = "http://www.w3.org/2001/XMLSchema#boolean";
    private const string JsonNull = "JSON_NULL";

    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    [Fact]
    public void Claim_NumericSre_ParsesToTheEpoch()
    {
        SessionRevocationEpochValidator.TryReadTokenEpoch(
            PrincipalWith(new Claim(SessionRevocationEpochValidator.ClaimType, "0", Integer32)), out var zero)
            .Should().BeTrue();
        zero.Should().Be(0);

        SessionRevocationEpochValidator.TryReadTokenEpoch(
            PrincipalWith(new Claim(SessionRevocationEpochValidator.ClaimType, "5", Integer32)), out var five)
            .Should().BeTrue();
        five.Should().Be(5);

        SessionRevocationEpochValidator.TryReadTokenEpoch(
            PrincipalWith(new Claim(SessionRevocationEpochValidator.ClaimType, "2147483648", Integer64)), out var big)
            .Should().BeTrue();
        big.Should().Be(2147483648);
    }

    [Fact]
    public void Claim_AbsentSre_TreatsAsLegacyEpochZero()
    {
        SessionRevocationEpochValidator.TryReadTokenEpoch(
            PrincipalWith(new Claim("sub", "some-account")), out var epoch)
            .Should().BeTrue();

        epoch.Should().Be(0);
    }

    [Fact]
    public void Claim_AbsentSre_OnNullPrincipal_TreatsAsEpochZero()
    {
        SessionRevocationEpochValidator.TryReadTokenEpoch(null, out var epoch)
            .Should().BeTrue();

        epoch.Should().Be(0);
    }

    [Fact]
    public void Claim_MalformedNonNumericString_FailsClosed()
    {
        SessionRevocationEpochValidator.TryReadTokenEpoch(
            PrincipalWith(new Claim(SessionRevocationEpochValidator.ClaimType, "abc", "http://www.w3.org/2001/XMLSchema#string")), out _)
            .Should().BeFalse();
    }

    [Fact]
    public void Claim_NumericStringRepresentation_FailsClosed()
    {
        // A quoted "5" is an arbitrary string serialization of the epoch, not
        // the agreed JSON numeric claim; it must fail closed.
        SessionRevocationEpochValidator.TryReadTokenEpoch(
            PrincipalWith(new Claim(SessionRevocationEpochValidator.ClaimType, "5", "http://www.w3.org/2001/XMLSchema#string")), out _)
            .Should().BeFalse();
    }

    [Fact]
    public void Claim_NonIntegralNumericValue_FailsClosed()
    {
        SessionRevocationEpochValidator.TryReadTokenEpoch(
            PrincipalWith(new Claim(SessionRevocationEpochValidator.ClaimType, "3.5", Double)), out _)
            .Should().BeFalse();
    }

    [Fact]
    public void Claim_PrecisionLosingOverflowNumeric_FailsClosed()
    {
        // A value beyond long range arrives as a double in scientific notation;
        // accepting it could bypass revocation, so it fails closed.
        SessionRevocationEpochValidator.TryReadTokenEpoch(
            PrincipalWith(new Claim(SessionRevocationEpochValidator.ClaimType, "9.223372036854776E+18", Double)), out _)
            .Should().BeFalse();
    }

    [Fact]
    public void Claim_BooleanSre_FailsClosed()
    {
        SessionRevocationEpochValidator.TryReadTokenEpoch(
            PrincipalWith(new Claim(SessionRevocationEpochValidator.ClaimType, "true", Boolean)), out _)
            .Should().BeFalse();
    }

    [Fact]
    public void Claim_JsonNullSre_FailsClosed()
    {
        SessionRevocationEpochValidator.TryReadTokenEpoch(
            PrincipalWith(new Claim(SessionRevocationEpochValidator.ClaimType, "", JsonNull)), out _)
            .Should().BeFalse();
    }

    [Fact]
    public void NegativeTokenEpoch_DoesNotBypassRevocation()
    {
        SessionRevocationEpochValidator.TryReadTokenEpoch(
            PrincipalWith(new Claim(SessionRevocationEpochValidator.ClaimType, "-1", Integer32)), out var negative)
            .Should().BeTrue();
        negative.Should().Be(-1);

        // At account epoch 0 a negative token epoch is strictly older.
        SessionRevocationEpochValidator.Evaluate(negative, accountEpoch: 0)
            .Should().Be(TokenEpochValidity.Revoked);
        SessionRevocationEpochValidator.Evaluate(negative, accountEpoch: 5)
            .Should().Be(TokenEpochValidity.Revoked);
    }

    [Fact]
    public void Validity_TokenEpochLessThanAccountEpoch_IsRevoked()
    {
        SessionRevocationEpochValidator.Evaluate(tokenEpoch: 1, accountEpoch: 2)
            .Should().Be(TokenEpochValidity.Revoked);
        SessionRevocationEpochValidator.Evaluate(tokenEpoch: 0, accountEpoch: 1)
            .Should().Be(TokenEpochValidity.Revoked);
    }

    [Fact]
    public void Validity_TokenEpochEqualToAccountEpoch_IsAccepted()
    {
        SessionRevocationEpochValidator.Evaluate(tokenEpoch: 0, accountEpoch: 0)
            .Should().Be(TokenEpochValidity.Accepted);
        SessionRevocationEpochValidator.Evaluate(tokenEpoch: 3, accountEpoch: 3)
            .Should().Be(TokenEpochValidity.Accepted);
    }

    [Fact]
    public void Validity_TokenEpochNewerThanAccountEpoch_IsAccepted()
    {
        // A token carrying a newer epoch may be accepted per the agreed
        // ADR-036 model; no synchronization mechanism is introduced in Q1.
        SessionRevocationEpochValidator.Evaluate(tokenEpoch: 5, accountEpoch: 0)
            .Should().Be(TokenEpochValidity.Accepted);
        SessionRevocationEpochValidator.Evaluate(tokenEpoch: 8, accountEpoch: 3)
            .Should().Be(TokenEpochValidity.Accepted);
    }

    [Fact]
    public void Validity_LegacyTokenWithoutSre_AcceptedAtZero_RevokedAfterAdvance()
    {
        // Legacy token (no sre claim) reads as epoch 0: accepted while the
        // account is at 0, rejected once the account has advanced past 0.
        var legacy = PrincipalWith(new Claim("sub", "some-account"));
        SessionRevocationEpochValidator.TryReadTokenEpoch(legacy, out var tokenEpoch).Should().BeTrue();
        tokenEpoch.Should().Be(0);

        SessionRevocationEpochValidator.Evaluate(tokenEpoch, accountEpoch: 0)
            .Should().Be(TokenEpochValidity.Accepted);
        SessionRevocationEpochValidator.Evaluate(tokenEpoch, accountEpoch: 1)
            .Should().Be(TokenEpochValidity.Revoked);
    }
}