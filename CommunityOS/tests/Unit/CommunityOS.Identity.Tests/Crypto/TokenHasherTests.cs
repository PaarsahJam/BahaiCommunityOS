using CommunityOS.Identity.Application.Crypto;
using FluentAssertions;

namespace CommunityOS.Identity.Tests.Crypto;

public sealed class TokenHasherTests
{
    [Fact]
    public void Hash_IsDeterministicHex()
    {
        var a = TokenHasher.Hash("some-token-value");
        var b = TokenHasher.Hash("some-token-value");

        a.Should().Be(b);
        a.Should().MatchRegex("^[0-9A-F]{64}$");
    }

    [Fact]
    public void Hash_IsIrreversible_ProducesDigestOfLength()
    {
        var hash = TokenHasher.Hash("token");

        hash.Length.Should().Be(64);
    }

    [Fact]
    public void Hash_IsCaseSensitiveToInput()
    {
        TokenHasher.Hash("abc").Should().NotBe(TokenHasher.Hash("Abc"));
    }

    [Fact]
    public void GenerateToken_ReturnsUniqueBase64Values()
    {
        var a = TokenHasher.GenerateToken();
        var b = TokenHasher.GenerateToken();

        a.Should().NotBe(b);
        a.Should().MatchRegex("^[A-Za-z0-9+/=]+$");
    }

    [Fact]
    public void GenerateToken_RespectsRequestedByteLength()
    {
        var token = TokenHasher.GenerateToken(16);

        var decoded = Convert.FromBase64String(token);
        decoded.Length.Should().Be(16);
    }
}
