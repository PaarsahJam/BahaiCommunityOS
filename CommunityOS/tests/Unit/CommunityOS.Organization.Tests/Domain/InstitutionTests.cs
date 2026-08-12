using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Enumerations;
using CommunityOS.Organization.Domain.ValueObjects;

namespace CommunityOS.Organization.Tests.Domain;

public class InstitutionTests
{
    private static Jurisdiction GlobalJurisdiction() => Jurisdiction.Global();

    [Fact]
    public void Create_initializes_active_institution()
    {
        var institution = Institution.Create("Universal House of Justice", "Institution", GlobalJurisdiction());

        institution.Id.Should().NotBeEmpty();
        institution.Name.Should().Be("Universal House of Justice");
        institution.IsActive.Should().BeTrue();
        institution.CreatedOn.Kind.Should().Be(DateTimeKind.Utc);
        institution.EstablishedOn.Should().BeNull();
    }

    [Fact]
    public void Create_trims_name()
    {
        var institution = Institution.Create("  Auxiliary Board  ", "Institution", GlobalJurisdiction());

        institution.Name.Should().Be("Auxiliary Board");
    }

    [Fact]
    public void Create_captures_established_on()
    {
        var established = new DateTime(1963, 4, 21, 0, 0, 0, DateTimeKind.Utc);

        var institution = Institution.Create("Universal House of Justice", "Institution", GlobalJurisdiction(), established);

        institution.EstablishedOn.Should().Be(established);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_name(string? name)
    {
        var act = () => Institution.Create(name!, "Institution", GlobalJurisdiction());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_changes_name_and_jurisdiction()
    {
        var institution = Institution.Create("Auxiliary Board", "Institution", GlobalJurisdiction());
        var newJurisdiction = Jurisdiction.Create(JurisdictionType.National, Guid.NewGuid());

        institution.UpdateDetails("Continental Board", newJurisdiction);

        institution.Name.Should().Be("Continental Board");
        institution.Jurisdiction.Should().Be(newJurisdiction);
    }

    [Fact]
    public void Deactivate_marks_inactive()
    {
        var institution = Institution.Create("Auxiliary Board", "Institution", GlobalJurisdiction());

        institution.Deactivate();

        institution.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_restores_active()
    {
        var institution = Institution.Create("Auxiliary Board", "Institution", GlobalJurisdiction());
        institution.Deactivate();

        institution.Activate();

        institution.IsActive.Should().BeTrue();
    }
}
