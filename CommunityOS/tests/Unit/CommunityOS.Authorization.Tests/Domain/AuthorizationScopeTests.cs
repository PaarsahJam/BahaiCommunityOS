using CommunityOS.Authorization.Domain.Enumerations;
using CommunityOS.Authorization.Domain.ValueObjects;

namespace CommunityOS.Authorization.Tests.Domain;

public class AuthorizationScopeTests
{
    [Fact]
    public void Global_scope_applies_only_to_global_checks()
    {
        var global = AuthorizationScope.Global();
        var orgCheck = AuthorizationScope.Scoped(ScopeType.Local, Guid.NewGuid());

        global.IsGlobal.Should().BeTrue();
        global.AppliesTo(AuthorizationScope.Global()).Should().BeTrue();
        global.AppliesTo(orgCheck).Should().BeFalse();
    }

    [Fact]
    public void Organization_scoped_scope_never_applies_to_global_checks()
    {
        var local = AuthorizationScope.Scoped(ScopeType.Local, Guid.NewGuid());
        local.AppliesTo(AuthorizationScope.Global()).Should().BeFalse();
    }

    [Fact]
    public void Organization_scopes_match_exact_type_and_id()
    {
        var orgId = Guid.NewGuid();
        var local = AuthorizationScope.Scoped(ScopeType.Local, orgId);
        var other = AuthorizationScope.Scoped(ScopeType.Local, Guid.NewGuid());
        var national = AuthorizationScope.Scoped(ScopeType.National, orgId);

        local.AppliesTo(AuthorizationScope.Scoped(ScopeType.Local, orgId)).Should().BeTrue();
        local.AppliesTo(other).Should().BeFalse();
        local.AppliesTo(national).Should().BeFalse();
    }

    [Fact]
    public void Scoped_rejects_global_and_resource_types()
    {
        var actGlobal = () => AuthorizationScope.Scoped(ScopeType.Global, Guid.NewGuid());
        var actResource = () => AuthorizationScope.Scoped(ScopeType.Resource, Guid.NewGuid());

        actGlobal.Should().Throw<ArgumentException>();
        actResource.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Resource_scope_requires_resource_type_and_matches_resource_exactly()
    {
        var recordId = Guid.NewGuid();
        var record = AuthorizationScope.Resource("record", recordId);

        record.AppliesTo(AuthorizationScope.Resource("record", recordId)).Should().BeTrue();
        record.AppliesTo(AuthorizationScope.Resource("document", recordId)).Should().BeFalse();
        record.AppliesTo(AuthorizationScope.Resource("record", Guid.NewGuid())).Should().BeFalse();
        record.AppliesTo(AuthorizationScope.Scoped(ScopeType.Local, recordId)).Should().BeFalse();
    }

    [Theory]
    [InlineData("National", true)]
    [InlineData("Regional", true)]
    [InlineData("Local", true)]
    [InlineData("OrganizationUnit", true)]
    [InlineData("Committee", true)]
    [InlineData("Global", false)]
    [InlineData("Resource", false)]
    public void IsOrganizationScoped_is_true_only_for_hierarchy_scopes(string typeName, bool expected)
    {
        var type = ScopeType.All.Single(t => t.Name == typeName);
        var scope = type switch
        {
            _ when type == ScopeType.Global => AuthorizationScope.Global(),
            _ when type == ScopeType.Resource => AuthorizationScope.Resource("record", Guid.NewGuid()),
            _ => AuthorizationScope.Scoped(type, Guid.NewGuid()),
        };

        scope.IsOrganizationScoped.Should().Be(expected);
    }

    [Fact]
    public void Value_equality_is_by_type_scopeId_and_resourceType()
    {
        var orgId = Guid.NewGuid();
        var a = AuthorizationScope.Scoped(ScopeType.Local, orgId);
        var b = AuthorizationScope.Scoped(ScopeType.Local, orgId);

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
