using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Exceptions;
using CommunityOS.Organization.Domain.ValueObjects;

namespace CommunityOS.Organization.Tests.Domain;

public class OrganizationUnitTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static EffectivePeriod OpenPeriod() =>
        EffectivePeriod.Create(Now.AddDays(-30));

    [Fact]
    public void Create_initializes_unit_with_root_parent()
    {
        var unit = OrganizationUnit.Create(Guid.NewGuid(), "Cluster 1", "Cluster", null, OpenPeriod());

        unit.Id.Should().NotBeEmpty();
        unit.Name.Should().Be("Cluster 1");
        unit.IsActive.Should().BeTrue();
        unit.Parents.Should().ContainSingle(p => p.ParentId == null);
    }

    [Fact]
    public void Create_records_parent_link()
    {
        var parentId = Guid.NewGuid();

        var unit = OrganizationUnit.Create(Guid.NewGuid(), "Unit A", "Institute", parentId, OpenPeriod());

        unit.Parents.Single().ParentId.Should().Be(parentId);
        unit.ParentIdAt(Now).Should().Be(parentId);
    }

    [Fact]
    public void ChangeParent_records_effective_dated_history()
    {
        var unit = OrganizationUnit.Create(Guid.NewGuid(), "Unit A", "Institute", null, OpenPeriod());
        var newParent = Guid.NewGuid();

        unit.ChangeParent(newParent, EffectivePeriod.Create(Now));

        unit.Parents.Should().HaveCount(2);
        unit.ParentIdAt(Now).Should().Be(newParent);
    }

    [Fact]
    public void ChangeParent_rejects_self_as_parent()
    {
        var unit = OrganizationUnit.Create(Guid.NewGuid(), "Unit A", "Institute", null, OpenPeriod());

        var act = () => unit.ChangeParent(unit.Id, EffectivePeriod.Create(Now));

        act.Should().Throw<HierarchyCycleException>();
    }

    [Fact]
    public void ChangeParent_rejects_unchanged_parent()
    {
        var parentId = Guid.NewGuid();
        var unit = OrganizationUnit.Create(Guid.NewGuid(), "Unit A", "Institute", parentId, OpenPeriod());

        var act = () => unit.ChangeParent(parentId, EffectivePeriod.Create(Now));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CurrentParent_respects_effective_dating()
    {
        var oldParent = Guid.NewGuid();
        var newParent = Guid.NewGuid();
        var unit = OrganizationUnit.Create(Guid.NewGuid(), "Unit A", "Institute", oldParent, OpenPeriod());
        var reparentedOn = Now.AddDays(-10);
        unit.ChangeParent(newParent, EffectivePeriod.Create(reparentedOn));

        unit.ParentIdAt(reparentedOn.AddMinutes(-1)).Should().Be(oldParent);
        unit.ParentIdAt(reparentedOn).Should().Be(newParent);
        unit.ParentIdAt(reparentedOn.AddMinutes(1)).Should().Be(newParent);
    }

    [Fact]
    public void Root_unit_has_no_current_parent()
    {
        var unit = OrganizationUnit.Create(Guid.NewGuid(), "Root", "Cluster", null, OpenPeriod());

        unit.ParentIdAt(Now).Should().BeNull();
    }

    [Fact]
    public void Deactivate_marks_inactive_and_is_not_repeatable()
    {
        var unit = OrganizationUnit.Create(Guid.NewGuid(), "Unit A", "Institute", null, OpenPeriod());

        unit.Deactivate();
        unit.IsActive.Should().BeFalse();

        var act = () => unit.Deactivate();
        act.Should().Throw<OrganizationUnitAlreadyDeactivatedException>();
    }

    [Fact]
    public void UpdateDetails_changes_name_and_type()
    {
        var unit = OrganizationUnit.Create(Guid.NewGuid(), "Unit A", "Institute", null, OpenPeriod());

        unit.UpdateDetails("Unit B", "Team");

        unit.Name.Should().Be("Unit B");
        unit.UnitType.Should().Be("Team");
    }
}
