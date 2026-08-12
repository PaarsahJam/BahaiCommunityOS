using CommunityOS.Organization.Domain.Enumerations;
using CommunityOS.Organization.Domain.ValueObjects;

namespace CommunityOS.Organization.Tests.Domain;

public class JurisdictionTests
{
    [Fact]
    public void Global_jurisdiction_requires_no_scope()
    {
        var global = Jurisdiction.Global();

        global.IsGlobal.Should().BeTrue();
        global.ScopeId.Should().BeNull();
    }

    [Fact]
    public void Create_global_ignores_scope_id()
    {
        var global = Jurisdiction.Create(JurisdictionType.Global, Guid.NewGuid());

        global.IsGlobal.Should().BeTrue();
        global.ScopeId.Should().BeNull();
    }

    [Fact]
    public void Create_scoped_requires_scope_id()
    {
        var act = () => Jurisdiction.Create(JurisdictionType.Local);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("National")]
    [InlineData("Regional")]
    [InlineData("Local")]
    [InlineData("OrganizationUnit")]
    [InlineData("Committee")]
    public void Create_scoped_preserves_type_and_scope(string typeName)
    {
        var type = JurisdictionType.All.Single(t => t.Name == typeName);
        var scopeId = Guid.NewGuid();

        var jurisdiction = Jurisdiction.Create(type, scopeId);

        jurisdiction.Type.Should().Be(type);
        jurisdiction.ScopeId.Should().Be(scopeId);
        jurisdiction.IsGlobal.Should().BeFalse();
    }

    [Fact]
    public void Value_equality_is_by_type_and_scope()
    {
        var scopeId = Guid.NewGuid();
        var a = Jurisdiction.Create(JurisdictionType.Local, scopeId);
        var b = Jurisdiction.Create(JurisdictionType.Local, scopeId);

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void ToString_encodes_type_and_scope()
    {
        var scopeId = Guid.NewGuid();
        var jurisdiction = Jurisdiction.Create(JurisdictionType.Local, scopeId);

        jurisdiction.ToString().Should().Be($"Local:{scopeId}");
        Jurisdiction.Global().ToString().Should().Be("Global");
    }
}
