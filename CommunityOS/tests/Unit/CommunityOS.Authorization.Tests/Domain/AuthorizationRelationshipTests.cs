using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Events;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Authorization.Domain.Relationships;

namespace CommunityOS.Authorization.Tests.Domain;

public class AuthorizationRelationshipTests
{
    private static readonly Guid Subject = Guid.NewGuid();
    private static readonly Guid ObjectId = Guid.NewGuid();

    [Fact]
    public void Create_raises_RelationshipWrittenEvent()
    {
        var relationship = AuthorizationRelationship.Create(Subject, "has_permission", "record", ObjectId, ["records.record.read"]);

        relationship.DomainEvents.Should().Contain(e => e is RelationshipWrittenEvent);
        relationship.GrantsPermission("records.record.read").Should().BeTrue();
        relationship.References(Subject, "has_permission", "record", ObjectId).Should().BeTrue();
    }

    [Fact]
    public void Create_without_permissions_grants_nothing()
    {
        var relationship = AuthorizationRelationship.Create(Subject, "belongs_to", "household", ObjectId);

        relationship.Permissions.Should().BeEmpty();
        relationship.GrantsPermission("records.record.read").Should().BeFalse();
    }

    [Fact]
    public void Invalid_relation_throws()
    {
        var act = () => AuthorizationRelationship.Create(Subject, "not a valid relation!", "record", ObjectId);

        act.Should().Throw<InvalidRelationException>();
    }

    [Fact]
    public void Invalid_permission_throws()
    {
        var act = () => AuthorizationRelationship.Create(Subject, "has_permission", "record", ObjectId, ["bad permission"]);

        act.Should().Throw<InvalidPermissionException>();
    }

    [Theory]
    [InlineData("has_permission", true)]
    [InlineData("belongs_to", true)]
    [InlineData("serves_on", true)]
    [InlineData("has_Permission", false)]
    [InlineData("has-permission", false)]
    [InlineData("", false)]
    public void RelationName_convention(string relation, bool valid)
        => RelationName.IsValid(relation).Should().Be(valid);
}
