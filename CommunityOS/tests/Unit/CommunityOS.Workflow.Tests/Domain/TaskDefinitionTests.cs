using CommunityOS.Workflow.Domain.Aggregates;
using CommunityOS.Workflow.Domain.Enumerations;
using CommunityOS.Workflow.Domain.Exceptions;

namespace CommunityOS.Workflow.Tests.Domain;

/// <summary>
/// Domain-invariant tests for the TaskDefinition aggregate (ADR-024): the
/// stable code, permitted outcome gating, update rules, and idempotent retire.
/// Definitions are configuration — never deleted, retired instead.
/// </summary>
public class TaskDefinitionTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static TaskDefinition CreateDefinition() =>
        TaskDefinition.Create(
            "record-review",
            "Record Review",
            "Human review of a community record.",
            WorkflowDomainTypes.Record,
            ["verified", "rejected"],
            dueIn: "P14D",
            requiresHumanReview: true,
            Actor,
            Now);

    [Fact]
    public void Create_normalizes_code_domain_type_and_outcomes()
    {
        var definition = TaskDefinition.Create(
            " RECORD-REVIEW ",
            "Record Review",
            "Human review of a community record.",
            "RECORD",
            ["Verified", "REJECTED"],
            "P14D", true, Actor, Now);

        definition.Code.Should().Be("RECORD-REVIEW");
        definition.DomainType.Should().Be("record");
        definition.PermittedOutcomeCodes.Should().Equal("verified", "rejected");
        definition.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_requires_at_least_one_permitted_outcome()
    {
        var act = () => TaskDefinition.Create(
            "record-review", "Record Review", "desc", "record", [], "P14D", true, Actor, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PermitsOutcome_is_case_insensitive()
    {
        var definition = CreateDefinition();

        definition.PermitsOutcome("VERIFIED").Should().BeTrue();
        definition.PermitsOutcome("rejected").Should().BeTrue();
        definition.PermitsOutcome("archived").Should().BeFalse();
    }

    [Fact]
    public void Update_replaces_outcomes_and_provenance()
    {
        var definition = CreateDefinition();

        definition.Update("Record Review", "Updated description.", "record",
            ["verified", "rejected", "clarification-requested"], "P30D", true, Actor, Now);

        definition.PermittedOutcomeCodes.Should()
            .Equal("verified", "rejected", "clarification-requested");
        definition.Description.Should().Be("Updated description.");
        definition.DueIn.Should().Be("P30D");
        definition.UpdatedBy.Should().Be(Actor);
    }

    [Fact]
    public void Retire_is_idempotent_and_blocks_updates()
    {
        var definition = CreateDefinition();

        definition.Retire(Actor, Now);
        definition.IsRetired.Should().BeTrue();

        definition.Retire(Actor, Now);
        definition.IsRetired.Should().BeTrue();

        var act = () => definition.Update("X", "Y", "record", ["verified"], null, true, Actor, Now);
        act.Should().Throw<RetiredTaskDefinitionUpdateException>();
    }

    [Fact]
    public void Baseline_codes_and_domain_types_are_valid()
    {
        foreach (var code in WorkflowDefinitionCodes.Baseline)
            code.Should().NotBeNullOrWhiteSpace();

        foreach (var domainType in new[]
                 {
                     WorkflowDomainTypes.Record,
                     WorkflowDomainTypes.Document,
                     WorkflowDomainTypes.KnowledgeQuestion,
                     WorkflowDomainTypes.KnowledgeAiSuggestion,
                     WorkflowDomainTypes.General
                 })
            WorkflowDomainTypes.IsValid(domainType).Should().BeTrue();
    }
}