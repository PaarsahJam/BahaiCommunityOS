using CommunityOS.Records.Domain.Aggregates;
using CommunityOS.Records.Domain.Exceptions;

namespace CommunityOS.Records.Tests.Domain;

/// <summary>
/// Domain-invariant tests for retention schedules (ADR-023): every rule period
/// and the optional maximum period must be a valid ISO-8601 duration, rules are
/// unique per category, and schedules are never versioned for deletion — retiring
/// is a reversible state.
/// </summary>
public class RetentionScheduleTests
{
    private static readonly Guid Actor = Guid.NewGuid();

    private static RetentionRule Rule(string period, string? maximumPeriod = null) =>
        RetentionRule.Create("birth", "recordDate", period, "review", null, maximumPeriod);

    [Fact]
    public void Create_accepts_an_iso8601_maximum_period()
    {
        var schedule = RetentionSchedule.Create(
            "schedule-a", "Schedule A", null, [Rule("P5Y", "P10Y")], Actor);

        schedule.Rules.Single().MaximumPeriod.Should().Be("P10Y");
    }

    [Fact]
    public void Create_rejects_a_non_iso8601_maximum_period()
    {
        var act = () => RetentionSchedule.Create(
            "schedule-a", "Schedule A", null, [Rule("P5Y", "ten years")], Actor);

        act.Should().Throw<InvalidRetentionPeriodException>();
    }

    [Fact]
    public void Create_rejects_a_non_iso8601_period()
    {
        var act = () => RetentionSchedule.Create(
            "schedule-a", "Schedule A", null, [Rule("5 years")], Actor);

        act.Should().Throw<InvalidRetentionPeriodException>();
    }

    [Fact]
    public void Create_rejects_duplicate_rules_for_the_same_category()
    {
        var act = () => RetentionSchedule.Create(
            "schedule-a",
            "Schedule A",
            null,
            [Rule("P5Y"), Rule("P10Y")],
            Actor);

        act.Should().Throw<ArgumentException>();
    }
}