using CommunityOS.Audit.Domain;
using FluentAssertions;
using Xunit;

namespace CommunityOS.Audit.Tests.Domain;

public sealed class AuditEntryTests
{
    [Fact]
    public void Create_produces_a_write_once_entry_with_all_fields_set()
    {
        var occurredOn = DateTime.UtcNow.AddMinutes(-1);
        var ingestedOn = DateTime.UtcNow;
        var resourceId = Guid.NewGuid();

        var entry = AuditEntry.Create("records", "RecordVerified", "record-verified",
            new string('b', 64), "record", resourceId, occurredOn, ingestedOn,
            AuditSensitivity.Normal, "standard-7y",
            outcome: null,
            secondaryResourceId: null,
            subjectId: Guid.NewGuid(),
            actorId: Guid.NewGuid(),
            organizationUnitId: Guid.NewGuid(),
            metadata: AuditMetadata.Create(new Dictionary<string, object?> { ["status"] = "Verified" }),
            retentionExpiresOn: occurredOn.AddYears(7));

        entry.Id.Should().NotBe(Guid.Empty);
        entry.SourceService.Should().Be("records");
        entry.SourceEventType.Should().Be("RecordVerified");
        entry.Action.Should().Be("record-verified");
        entry.ResourceId.Should().Be(resourceId);
        entry.RetentionExpiresOn.Should().Be(occurredOn.AddYears(7));
        entry.MetadataJson.Should().Contain("status");
    }

    [Fact]
    public void Create_rejects_invalid_field_contracts()
    {
        var validHash = new string('c', 64);
        Create(() => AuditEntry.Create("", "E", "a", validHash, "record", Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow, AuditSensitivity.Normal, "k"))
            .Should().Throw<ArgumentException>();
        Create(() => AuditEntry.Create("records", "", "a", validHash, "record", Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow, AuditSensitivity.Normal, "k"))
            .Should().Throw<ArgumentException>();
        Create(() => AuditEntry.Create("records", "E", "", validHash, "record", Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow, AuditSensitivity.Normal, "k"))
            .Should().Throw<ArgumentException>();
        Create(() => AuditEntry.Create("records", "E", "a", "nothex", "record", Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow, AuditSensitivity.Normal, "k"))
            .Should().Throw<ArgumentException>();
        Create(() => AuditEntry.Create("records", "E", "a", validHash, "record", Guid.Empty, DateTime.UtcNow, DateTime.UtcNow, AuditSensitivity.Normal, "k"))
            .Should().Throw<ArgumentException>();
        Create(() => AuditEntry.Create("records", "E", "a", validHash, "record", Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow, AuditSensitivity.Normal, ""))
            .Should().Throw<ArgumentException>();
        Create(() => AuditEntry.Create("records", "E", "a", validHash, "record", Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow, AuditSensitivity.Normal, "k", secondaryResourceId: Guid.Empty))
            .Should().Throw<ArgumentException>();
    }

    private static Action Create(Func<AuditEntry> factory) => () => factory();
}

public sealed class AuditEntryHoldTests
{
    [Fact]
    public void Places_and_releases_a_hold_exactly_once()
    {
        var placedBy = Guid.NewGuid();
        var placedOn = new DateTime(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc);
        var hold = AuditEntryHold.Create(Guid.NewGuid(), AuditEntryHold.Legal, "investigation", placedBy, placedOn);

        hold.IsActive.Should().BeTrue();
        hold.ReleasedOn.Should().BeNull();

        var releasedBy = Guid.NewGuid();
        var releasedOn = placedOn.AddHours(2);
        hold.Release(releasedBy, releasedOn);

        hold.IsActive.Should().BeFalse();
        hold.ReleasedBy.Should().Be(releasedBy);
        hold.ReleasedOn.Should().Be(releasedOn);

        var secondRelease = () => hold.Release(Guid.NewGuid(), releasedOn.AddMinutes(1));
        secondRelease.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rejects_unknown_hold_types_and_reason_codes()
    {
        var now = DateTime.UtcNow;
        var entryId = Guid.NewGuid();

        FluentActions.Invoking(() => AuditEntryHold.Create(entryId, "temporary", "investigation", Guid.NewGuid(), now))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => AuditEntryHold.Create(entryId, AuditEntryHold.Administrative, "because", Guid.NewGuid(), now))
            .Should().Throw<ArgumentException>();

        AuditEntryHold.ReasonCodes.Should().BeEquivalentTo(
            ["investigation", "legal-request", "dispute", "regulatory-inquiry", "other"]);
    }
}
