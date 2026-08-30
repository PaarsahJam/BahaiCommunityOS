using System.Text.Json;
using CommunityOS.Audit.Domain;
using FluentAssertions;
using Xunit;

namespace CommunityOS.Audit.Tests.Domain;

public sealed class AuditMetadataTests
{
    [Fact]
    public void Accepts_allowlisted_scalar_values()
    {
        var metadata = AuditMetadata.Create(new Dictionary<string, object?>
        {
            ["category"] = "membership",
            ["status"] = "Draft",
            ["recipient_count"] = 42,
            ["version_number"] = 3
        });

        metadata.Values.Should().HaveCount(4);
        var parsed = JsonDocument.Parse(metadata.Json);
        parsed.RootElement.GetProperty("category").GetString().Should().Be("membership");
        parsed.RootElement.GetProperty("recipient_count").GetString().Should().Be("42");
    }

    [Fact]
    public void Accepts_the_authorization_metadata_keys_ratified_for_this_gate()
    {
        var metadata = AuditMetadata.Create(new Dictionary<string, object?>
        {
            ["role_code"] = "treasurer",
            ["scope_type"] = "Local"
        });

        metadata.Values.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["role_code"] = "treasurer",
            ["scope_type"] = "Local"
        });
    }

    [Fact]
    public void Rejects_keys_outside_the_ratified_allowlist()
    {
        var create = () => AuditMetadata.Create(new Dictionary<string, object?> { ["title"] = "nope" });
        create.Should().Throw<ArgumentException>().WithMessage("*allowlist*");
    }

    [Fact]
    public void Rejects_more_than_ten_keys()
    {
        var keys = new[] { "category", "status", "subject_type", "definition_code", "domain_type",
            "hold_id", "hold_type", "reason_code", "type_code", "channel", "source_type" };
        keys.Length.Should().BeGreaterThan(AuditMetadata.MaxKeys);
        var dict = keys.ToDictionary(k => k, k => (object?)"x");
        var create = () => AuditMetadata.Create(dict);
        create.Should().Throw<ArgumentException>().WithMessage("*10*");
    }

    [Fact]
    public void Rejects_null_empty_and_oversized_values()
    {
        var createNull = () => AuditMetadata.Create(new Dictionary<string, object?> { ["status"] = null! });
        var createEmpty = () => AuditMetadata.Create(new Dictionary<string, object?> { ["status"] = "" });
        var createOversized = () => AuditMetadata.Create(new Dictionary<string, object?> { ["status"] = new string('x', 201) });
        createNull.Should().Throw<ArgumentException>();
        createEmpty.Should().Throw<ArgumentException>();
        createOversized.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromJson_round_trips_and_revalidates()
    {
        var original = AuditMetadata.Create(new Dictionary<string, object?> { ["channel"] = "email", ["recipient_count"] = 7 });
        var restored = AuditMetadata.FromJson(original.Json);
        restored.Values["channel"].Should().Be("email");
        restored.Values["recipient_count"].Should().Be("7");

        var invalid = () => AuditMetadata.FromJson("""{"unknown_key":"x"}""");
        invalid.Should().Throw<ArgumentException>();
    }
}
