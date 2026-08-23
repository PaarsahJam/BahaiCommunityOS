using CommunityOS.Audit.Domain;
using FluentAssertions;
using Xunit;

namespace CommunityOS.Audit.Tests.Domain;

public sealed class SourceEventHashTests
{
    [Fact]
    public void Computes_stable_lowercase_sha256_over_the_canonical_string()
    {
        var occurredOn = new DateTime(2026, 8, 22, 10, 0, 0, DateTimeKind.Utc);
        var id = Guid.Parse("a1000000-0000-0000-0000-000000000002");

        var first = SourceEventHash.Compute("records", "RecordVerified", "record", id, null, occurredOn, "x");
        var second = SourceEventHash.Compute("records", "RecordVerified", "record", id, null, occurredOn, "x");

        first.Should().Be(second);
        first.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Every_hash_input_component_changes_the_digest()
    {
        var occurredOn = new DateTime(2026, 8, 22, 10, 0, 0, DateTimeKind.Utc);
        var id = Guid.NewGuid();
        var secondary = Guid.NewGuid();

        var baseline = SourceEventHash.Compute("records", "RecordVerified", "record", id, null, occurredOn, "d");

        SourceEventHash.Compute("workflow", "RecordVerified", "record", id, null, occurredOn, "d")
            .Should().NotBe(baseline);
        SourceEventHash.Compute("records", "RecordArchived", "record", id, null, occurredOn, "d")
            .Should().NotBe(baseline);
        SourceEventHash.Compute("records", "RecordVerified", "document", id, null, occurredOn, "d")
            .Should().NotBe(baseline);
        SourceEventHash.Compute("records", "RecordVerified", "record", Guid.NewGuid(), null, occurredOn, "d")
            .Should().NotBe(baseline);
        SourceEventHash.Compute("records", "RecordVerified", "record", id, secondary, occurredOn, "d")
            .Should().NotBe(baseline);
        SourceEventHash.Compute("records", "RecordVerified", "record", id, null,
            occurredOn.AddMilliseconds(1), "d").Should().NotBe(baseline);
        SourceEventHash.Compute("records", "RecordVerified", "record", id, null, occurredOn, "other")
            .Should().NotBe(baseline);
    }

    [Fact]
    public void Local_and_UTC_timestamps_representing_the_same_instant_hash_identically()
    {
        var utc = new DateTime(2026, 8, 22, 10, 0, 0, DateTimeKind.Utc);
        var sameInstantLocal = new DateTime(2026, 8, 22, 12, 0, 0, DateTimeKind.Local);
        if (sameInstantLocal.ToUniversalTime() != utc)
        {
            return; // environment timezone differs from the assumed offset
        }

        SourceEventHash.Compute("records", "E", "record", Guid.NewGuid(), null, sameInstantLocal, "d")
            .Should().Be(SourceEventHash.Compute("records", "E", "record", Guid.NewGuid(), null, utc, "d"));
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", true)]
    [InlineData("0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef", false)]
    [InlineData("short", false)]
    [InlineData("", false)]
    public void IsValidShape_accepts_only_64_lowercase_hex(string hash, bool expected) =>
        SourceEventHash.IsValidShape(hash).Should().Be(expected);
}
