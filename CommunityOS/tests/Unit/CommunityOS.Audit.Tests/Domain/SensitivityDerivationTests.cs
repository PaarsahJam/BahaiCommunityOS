using CommunityOS.Audit.Domain;
using FluentAssertions;
using Xunit;

namespace CommunityOS.Audit.Tests.Domain;

public sealed class SensitivityDerivationTests
{
    [Fact]
    public void Ratified_event_type_list_is_always_sensitive()
    {
        foreach (var eventType in new[] { "RecordHoldPlaced", "RecordHoldReleased" })
        {
            SensitiveAuditEventTypes.Resolve(eventType, payloadIsSensitive: false)
                .Should().Be(AuditSensitivity.Sensitive, $"{eventType} is on the ratified list");
        }
    }

    [Fact]
    public void Payload_sensitivity_flag_promotes_only_when_set()
    {
        SensitiveAuditEventTypes.Resolve("RecordClassified", payloadIsSensitive: true)
            .Should().Be(AuditSensitivity.Sensitive);
        SensitiveAuditEventTypes.Resolve("RecordClassified", payloadIsSensitive: false)
            .Should().Be(AuditSensitivity.Normal);
    }

    [Theory]
    [InlineData("RecordCreated")]
    [InlineData("RecordVerified")]
    [InlineData("WorkflowTaskCompleted")]
    [InlineData("NotificationDispatched")]
    public void Everything_else_is_normal(string eventType) =>
        SensitiveAuditEventTypes.Resolve(eventType, payloadIsSensitive: false)
            .Should().Be(AuditSensitivity.Normal);

    [Fact]
    public void The_ratified_list_carries_exactly_the_first_gate_members()
    {
        // First gate consumes only the Records hold pair; the full ADR list
        // also names break-glass and document-download events for later gates.
        SensitiveAuditEventTypes.All.Should().Contain(
            ["BreakGlassRequested", "BreakGlassApproved", "BreakGlassRevoked",
             "RecordHoldPlaced", "RecordHoldReleased", "DocumentContentDownloaded"]);
    }
}
