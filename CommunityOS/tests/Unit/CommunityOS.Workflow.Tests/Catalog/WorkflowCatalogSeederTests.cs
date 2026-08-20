using CommunityOS.Workflow.Domain.Enumerations;
using CommunityOS.Workflow.Infrastructure.Persistence;

namespace CommunityOS.Workflow.Tests.Catalog;

/// <summary>
/// Locks the ratified baseline task-definition catalog (ADR-024). The catalog
/// is configuration — never a hard enum — but the baseline codes must always be
/// present so a fresh database can create tasks immediately.
/// </summary>
public class WorkflowCatalogSeederTests
{
    private static readonly string[] BaselineCodes =
    [
        "record-review", "document-review", "knowledge-moderation",
        "knowledge-ai-review", "general"
    ];

    [Fact]
    public void Baseline_contains_the_five_ratified_codes()
    {
        WorkflowCatalogSeeder.Baseline.Select(b => b.Code).Should().BeEquivalentTo(BaselineCodes);
    }

    [Fact]
    public void Baseline_codes_are_stable_and_unique()
    {
        WorkflowCatalogSeeder.Baseline.Select(b => b.Code)
            .Should().OnlyHaveUniqueItems()
            .And.AllSatisfy(code => code.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void Baseline_actor_is_a_stable_non_person_identifier()
    {
        WorkflowCatalogSeeder.BaselineActorId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Every_baseline_code_is_a_canonical_definition_code()
    {
        foreach (var code in BaselineCodes)
            code.Should().BeOneOf(WorkflowDefinitionCodes.Baseline);
    }

    [Fact]
    public void Every_baseline_domain_type_is_valid()
    {
        foreach (var (_, _, _, domainType, _, _) in WorkflowCatalogSeeder.Baseline)
            WorkflowDomainTypes.IsValid(domainType).Should().BeTrue();
    }

    [Fact]
    public void Baseline_uses_the_ratified_due_in_default()
    {
        WorkflowCatalogSeeder.Baseline.Should().OnlyContain(b => b.DueIn == WorkflowDefaults.DueIn);
    }
}