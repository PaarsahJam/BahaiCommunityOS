using CommunityOS.Notifications.Domain.ValueObjects;
using CommunityOS.Notifications.Infrastructure.Integration.Workflow;
using CommunityOS.Notifications.Infrastructure.Persistence;

namespace CommunityOS.Notifications.Tests.Catalog;

/// <summary>
/// Locks the ratified baseline notification-type catalog (ADR-025). The catalog
/// is configuration — never a hard enum — but the baseline codes must always be
/// present so a fresh database can dispatch notifications immediately, and
/// <c>record-hold</c> is the only sensitive baseline type.
/// </summary>
public class NotificationsCatalogSeederTests
{
    private static readonly string[] BaselineCodes =
    [
        "task-assigned", "task-escalated", "task-completed", "task-cancelled",
        "record-verified", "record-rejected", "record-hold",
        "question-flagged", "community-activity", "community-event", "community-meeting",
        "general"
    ];

    [Fact]
    public void Baseline_contains_the_twelve_ratified_codes()
    {
        NotificationsCatalogSeeder.Baseline.Select(b => b.Code).Should().BeEquivalentTo(BaselineCodes);
    }

    [Fact]
    public void Baseline_codes_are_stable_unique_and_lowercase()
    {
        NotificationsCatalogSeeder.Baseline.Select(b => b.Code)
            .Should().OnlyHaveUniqueItems()
            .And.AllSatisfy(code => code.Should().MatchRegex("^[a-z0-9-]+$"));
    }

    [Fact]
    public void Record_hold_is_the_only_sensitive_baseline_type()
    {
        NotificationsCatalogSeeder.Baseline
            .Where(b => b.IsSensitive).Select(b => b.Code)
            .Should().Equal("record-hold");
    }

    [Fact]
    public void Every_baseline_type_uses_a_ratified_channel()
    {
        foreach (var (_, _, channel, _, _, _) in NotificationsCatalogSeeder.Baseline)
            NotificationChannel.All.Should().Contain(channel);
    }

    [Fact]
    public void Baseline_actor_is_a_stable_non_person_identifier()
    {
        NotificationsCatalogSeeder.BaselineActorId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Workflow_task_codes_match_the_reconcile_consumer_constants()
    {
        NotificationsCatalogSeeder.Baseline.Select(b => b.Code)
            .Should().Contain(NotificationTypeCodes.TaskAssigned)
            .And.Contain(NotificationTypeCodes.TaskEscalated);
    }
}