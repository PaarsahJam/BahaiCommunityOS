using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Enumerations;
using CommunityOS.Authorization.Domain.Events;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Authorization.Domain.ValueObjects;

namespace CommunityOS.Authorization.Tests.Domain;

public class RoleTests
{
    [Fact]
    public void Create_normalizes_and_deduplicates_permissions()
    {
        var role = Role.Create("editor", "Editor", "Can edit records", ["records.record.read", "records.record.read", "records.record.update"]);

        role.Code.Should().Be("editor");
        role.Enabled.Should().BeTrue();
        role.IsSystem.Should().BeFalse();
        role.Permissions.Should().BeEquivalentTo(["records.record.read", "records.record.update"]);
        role.HasPermission("records.record.read").Should().BeTrue();
    }

    [Fact]
    public void Create_raises_RoleCreatedEvent()
    {
        var role = Role.Create("editor", "Editor", null, ["records.record.read"]);

        role.DomainEvents.Should().ContainSingle(e => e is RoleCreatedEvent);
    }

    [Fact]
    public void Create_rejects_invalid_permissions()
    {
        var act = () => Role.Create("editor", "Editor", null, ["not-a-valid-permission"]);

        act.Should().Throw<InvalidPermissionException>();
    }

    [Fact]
    public void Create_rejects_blank_code_and_display_name()
    {
        var actCode = () => Role.Create(" ", "Editor", null, ["records.record.read"]);
        var actName = () => Role.Create("editor", " ", null, ["records.record.read"]);

        actCode.Should().Throw<ArgumentException>();
        actName.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_changes_display_name_and_description()
    {
        var role = Role.Create("editor", "Editor", "Old", ["records.record.read"]);

        role.UpdateDetails("Content Editor", "New description");

        role.DisplayName.Should().Be("Content Editor");
        role.Description.Should().Be("New description");
    }

    [Fact]
    public void UpdatePermissions_replaces_and_normalizes()
    {
        var role = Role.Create("editor", "Editor", null, ["records.record.read"]);

        role.UpdatePermissions(["records.record.read", "records.record.delete"]);

        role.HasPermission("records.record.delete").Should().BeTrue();
        role.HasPermission("records.record.update").Should().BeFalse();
    }

    [Fact]
    public void Disable_then_Enable_toggles_Enabled()
    {
        var role = Role.Create("editor", "Editor", null, ["records.record.read"]);

        role.Disable();
        role.Enabled.Should().BeFalse();

        role.Enable();
        role.Enabled.Should().BeTrue();
    }
}
