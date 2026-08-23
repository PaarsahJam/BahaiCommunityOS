using CommunityOS.Audit.Application;
using CommunityOS.Audit.Infrastructure.Retention;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CommunityOS.Audit.Tests.Application;

public sealed class RetentionPolicyTests
{
    private static AuditOptions NewOptions() => new()
    {
        Retention = new AuditRetentionOptions
        {
            DefaultClass = "default",
            Classes = new Dictionary<string, string?>
            {
                ["default"] = "",
                ["standard-7y"] = "P7Y",
                ["operational-3y"] = "P3Y"
            },
            EventClasses = new Dictionary<string, string>
            {
                ["RecordCreated"] = "standard-7y",
                ["WorkflowTaskCreated"] = "operational-3y"
            }
        }
    };

    private static AuditRetentionPolicy Policy(AuditOptions? options = null) =>
        new(Options.Create(options ?? NewOptions()));

    [Fact]
    public void Mapped_event_types_receive_their_class_duration_from_event_time()
    {
        var occurredOn = new DateTime(2026, 8, 22, 0, 0, 0, DateTimeKind.Utc);

        var assignment = Policy().Assign("RecordCreated", occurredOn);

        assignment.Class.Should().Be("standard-7y");
        assignment.ExpiresOn.Should().Be(occurredOn.AddYears(7));
    }

    [Fact]
    public void Unmapped_event_types_fall_back_to_the_default_indefinite_class()
    {
        var assignment = Policy().Assign("SomeUnratifiedEvent", DateTime.UtcNow);

        assignment.Class.Should().Be("default");
        assignment.ExpiresOn.Should().BeNull();
    }

    [Fact]
    public void Classes_without_a_duration_retain_indefinitely()
    {
        var options = NewOptions();
        options.Retention.EventClasses["NotificationDispatched"] = "default";

        var assignment = Policy(options).Assign("NotificationDispatched", DateTime.UtcNow);

        assignment.Class.Should().Be("default");
        assignment.ExpiresOn.Should().BeNull();
    }

    [Fact]
    public void Malformed_durations_fail_fast_at_construction()
    {
        var options = NewOptions();
        options.Retention.Classes["standard-7y"] = "seven-years";

        var create = () => Policy(options);
        create.Should().Throw<InvalidOperationException>().WithMessage("*ISO 8601*");
    }

    [Fact]
    public void Journal_class_assignments_are_configuration_driven()
    {
        // The audit-of-audit entries pass their class explicitly; the policy
        // only serves producer events. This test documents the seam.
        var policy = Policy();
        policy.Assign("AuditEntriesExported", DateTime.UtcNow).Class.Should().Be("default");
    }
}
