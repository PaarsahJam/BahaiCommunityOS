using CommunityOS.Identity.Infrastructure.Security;
using FluentAssertions;
using System.Globalization;

namespace CommunityOS.Identity.Tests.Security;

public sealed class Pbkdf2PasswordHasherTests
{
    [Fact]
    public void Hash_ProducesExpectedFormat()
    {
        var sut = new Pbkdf2PasswordHasher();

        var hash = sut.Hash("correct horse battery staple");

        var parts = hash.Split('$');
        parts.Should().HaveCount(4);
        parts[0].Should().Be("PBKDF2-SHA256");
        int.Parse(parts[1], CultureInfo.InvariantCulture).Should().BeGreaterThan(100_000);
    }

    [Fact]
    public void Hash_GeneratesUniqueSaltPerCall()
    {
        var sut = new Pbkdf2PasswordHasher();

        var a = sut.Hash("same-password");
        var b = sut.Hash("same-password");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Verify_ReturnsTrueForCorrectPassword()
    {
        var sut = new Pbkdf2PasswordHasher();

        var hash = sut.Hash("correct horse battery staple");

        sut.Verify("correct horse battery staple", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_ReturnsFalseForIncorrectPassword()
    {
        var sut = new Pbkdf2PasswordHasher();

        var hash = sut.Hash("correct horse battery staple");

        sut.Verify("wrong-password", hash).Should().BeFalse();
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("PBKDF2-SHA256$210000$c2FsdA$dmFsdWU")]
    public void Verify_ReturnsFalseForMalformedHash(string hash)
    {
        var sut = new Pbkdf2PasswordHasher();

        sut.Verify("any-password", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_ThrowsOnNullHash()
    {
        var sut = new Pbkdf2PasswordHasher();

        var act = () => sut.Verify("password", null!);

        act.Should().Throw<ArgumentException>();
    }
}
