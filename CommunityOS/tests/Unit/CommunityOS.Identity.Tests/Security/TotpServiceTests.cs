using CommunityOS.Identity.Infrastructure.Security;
using FluentAssertions;
using System.Globalization;

namespace CommunityOS.Identity.Tests.Security;

public sealed class TotpServiceTests
{
    private const string Rfc6238Secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Fact]
    public void GenerateSecret_ReturnsBase32EncodedKey()
    {
        var sut = new TotpService();

        var secret = sut.GenerateSecret();

        secret.Should().NotBeNullOrWhiteSpace();
        secret.All(c => "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".Contains(c)).Should().BeTrue();
    }

    [Theory]
    [InlineData("1970-01-01T00:00:59Z", "287082")]
    [InlineData("2005-03-18T01:58:29Z", "081804")]
    [InlineData("2005-03-18T01:58:31Z", "050471")]
    [InlineData("2009-02-13T23:31:30Z", "005924")]
    [InlineData("2033-05-18T03:33:20Z", "279037")]
    public void ComputeCode_MatchesRfc6238Sha1TestVectors(string timestampUtc, string expected)
    {
        var sut = new TotpService();
        var timestamp = DateTime.Parse(
            timestampUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

        var code = sut.ComputeCode(Rfc6238Secret, timestamp);

        code.Should().Be(expected);
    }

    [Fact]
    public void Verify_AcceptsCodeWithinClockDriftWindow()
    {
        var sut = new TotpService();
        var secret = sut.GenerateSecret();

        var code = sut.ComputeCode(secret, DateTime.UtcNow);

        sut.Verify(secret, code, acceptedClockDrift: 1).Should().BeTrue();
    }

    [Fact]
    public void Verify_RejectsIncorrectCode()
    {
        var sut = new TotpService();
        var secret = sut.GenerateSecret();

        var valid = sut.ComputeCode(secret, DateTime.UtcNow);
        var wrong = valid == "000000" ? "000001" : "000000";

        sut.Verify(secret, wrong).Should().BeFalse();
    }

    [Fact]
    public void Verify_RejectsEmptyCode()
    {
        var sut = new TotpService();

        var act = () => sut.Verify("SECRET", "");

        act.Should().Throw<ArgumentException>();
    }
}
