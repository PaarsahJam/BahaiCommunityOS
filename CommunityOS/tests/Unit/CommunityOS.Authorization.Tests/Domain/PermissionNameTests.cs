using CommunityOS.Authorization.Domain.Permissions;

namespace CommunityOS.Authorization.Tests.Domain;

public class PermissionNameTests
{
    [Theory]
    [InlineData("records.record.read")]
    [InlineData("records.record.update")]
    [InlineData("authz.role.assign")]
    [InlineData("a.b")]
    [InlineData("community.members.member.view")]
    public void IsValid_returns_true_for_conforming_names(string permission)
        => PermissionName.IsValid(permission).Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("nopermission")]
    [InlineData("no-dot")]
    [InlineData("record.read ")]
    [InlineData(" Record.read")]
    [InlineData("records.record.READ")]
    [InlineData("records..read")]
    [InlineData("1records.record.read")]
    [InlineData(".record.read")]
    [InlineData("records.record.")]
    public void IsValid_returns_false_for_nonconforming_names(string? permission)
        => PermissionName.IsValid(permission).Should().BeFalse();

    [Fact]
    public void IsValid_rejects_overlong_names()
    {
        var longName = string.Join('.', Enumerable.Repeat("abc", 64));
        longName.Length.Should().BeGreaterThan(PermissionName.MaxLength);
        PermissionName.IsValid(longName).Should().BeFalse();
    }

    [Theory]
    [InlineData("  records.record.read", "records.record.read")]
    [InlineData("records.record.read  ", "records.record.read")]
    public void Normalize_trims_surrounding_whitespace(string input, string expected)
        => PermissionName.Normalize(input).Should().Be(expected);

    [Fact]
    public void Normalize_rejects_empty()
    {
        var act = () => PermissionName.Normalize("  ");
        act.Should().Throw<ArgumentException>();
    }
}
