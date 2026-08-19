using CommunityOS.Records.Infrastructure.Persistence;

namespace CommunityOS.Records.Tests.Catalog;

/// <summary>
/// Locks the ratified baseline category catalog (ADR-023). The catalog is
/// configuration — never a hard enum — but the baseline codes must always be
/// present so a fresh database can create records immediately.
/// </summary>
public class RecordsCatalogSeederTests
{
    private static readonly string[] BaselineCodes =
    [
        "birth", "marriage", "death", "membership", "appointment",
        "official-community", "administrative"
    ];

    [Fact]
    public void Baseline_contains_the_seven_ratified_codes()
    {
        RecordsCatalogSeeder.Baseline.Select(b => b.Code).Should().BeEquivalentTo(BaselineCodes);
    }

    [Fact]
    public void Baseline_codes_are_stable_and_unique()
    {
        RecordsCatalogSeeder.Baseline.Select(b => b.Code)
            .Should().OnlyHaveUniqueItems()
            .And.AllSatisfy(code => code.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void Baseline_actor_is_a_stable_non_person_identifier()
    {
        RecordsCatalogSeeder.BaselineActorId.Should().NotBe(Guid.Empty);
    }
}