using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Enumerations;
using CommunityOS.Organization.Domain.ValueObjects;

namespace CommunityOS.Organization.Tests.Domain;

using Organization = CommunityOS.Organization.Domain.Aggregates.Organization;

public class OrganizationTests
{
    private static Jurisdiction LocalJurisdiction() =>
        Jurisdiction.Create(JurisdictionType.Local, Guid.NewGuid());

    [Fact]
    public void Create_initializes_active_organization()
    {
        var org = Organization.Create("Ridvan Cluster", "LocalSpiritualAssembly", LocalJurisdiction());

        org.Id.Should().NotBeEmpty();
        org.Name.Should().Be("Ridvan Cluster");
        org.Status.Should().Be("active");
        org.CreatedOn.Kind.Should().Be(DateTimeKind.Utc);
        org.DissolvedOn.Should().BeNull();
    }

    [Fact]
    public void Create_trims_name()
    {
        var org = Organization.Create("  Ridvan Cluster  ", "LocalSpiritualAssembly", LocalJurisdiction());

        org.Name.Should().Be("Ridvan Cluster");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_name(string? name)
    {
        var act = () => Organization.Create(name!, "LocalSpiritualAssembly", LocalJurisdiction());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_missing_jurisdiction()
    {
        var act = () => Organization.Create("Ridvan Cluster", "LocalSpiritualAssembly", null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Dissolve_marks_status_and_sets_dissolved_on()
    {
        var org = Organization.Create("Ridvan Cluster", "LocalSpiritualAssembly", LocalJurisdiction());
        var moment = DateTime.UtcNow;

        org.Dissolve(moment);

        org.Status.Should().Be("dissolved");
        org.DissolvedOn.Should().BeCloseTo(moment, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Dissolve_is_idempotent()
    {
        var org = Organization.Create("Ridvan Cluster", "LocalSpiritualAssembly", LocalJurisdiction());

        org.Dissolve(DateTime.UtcNow);
        var dissolvedOn = org.DissolvedOn;
        org.Dissolve(DateTime.UtcNow.AddDays(1));

        org.Status.Should().Be("dissolved");
        org.DissolvedOn.Should().Be(dissolvedOn);
    }

    [Fact]
    public void Suspend_sets_suspended_status()
    {
        var org = Organization.Create("Ridvan Cluster", "LocalSpiritualAssembly", LocalJurisdiction());

        org.Suspend();

        org.Status.Should().Be("suspended");
    }

    [Fact]
    public void Activate_restores_active_status()
    {
        var org = Organization.Create("Ridvan Cluster", "LocalSpiritualAssembly", LocalJurisdiction());
        org.Suspend();

        org.Activate();

        org.Status.Should().Be("active");
    }

    [Fact]
    public void UpdateDetails_changes_name_and_jurisdiction()
    {
        var org = Organization.Create("Ridvan Cluster", "LocalSpiritualAssembly", LocalJurisdiction());
        var newJurisdiction = Jurisdiction.Create(JurisdictionType.National, Guid.NewGuid());

        org.UpdateDetails("National Assembly", newJurisdiction);

        org.Name.Should().Be("National Assembly");
        org.Jurisdiction.Should().Be(newJurisdiction);
    }
}
