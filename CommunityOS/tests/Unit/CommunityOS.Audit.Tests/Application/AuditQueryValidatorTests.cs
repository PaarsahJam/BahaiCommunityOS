using CommunityOS.Audit.Application;
using CommunityOS.Audit.Application.Validators;
using FluentAssertions;
using Xunit;

namespace CommunityOS.Audit.Tests.Application;

public sealed class AuditQueryValidatorTests
{
    private static readonly AuditOptions Options = new();

    [Fact]
    public void Query_rejects_unknown_order_and_negative_paging()
    {
        var validator = new AuditQueryValidator();
        var actor = Guid.NewGuid();
        var filters = new AuditQueryFilters(null, null, null, null, null, null, null, null, null, null);

        validator.Validate(new AuditQuery(actor, filters, "sideways", false, 10, 0)).IsValid.Should().BeFalse();
        validator.Validate(new AuditQuery(actor, filters, "desc", false, 0, 0)).IsValid.Should().BeFalse();
        validator.Validate(new AuditQuery(actor, filters, "desc", false, 10, -1)).IsValid.Should().BeFalse();
        validator.Validate(new AuditQuery(Guid.Empty, filters, "desc", false, 10, 0)).IsValid.Should().BeFalse();

        validator.Validate(new AuditQuery(actor, filters, "asc", false, null, 0)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Query_rejects_inverted_date_ranges()
    {
        var validator = new AuditQueryValidator();
        var actor = Guid.NewGuid();
        var from = new DateTime(2026, 8, 22, 0, 0, 0, DateTimeKind.Utc);
        var filters = new AuditQueryFilters(null, null, null, null, null, null, null, null,
            from, from.AddDays(-1));

        validator.Validate(new AuditQuery(actor, filters, "desc", false, 10, 0)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void PlaceHold_requires_batched_ids_legal_types_and_ratified_reason_codes()
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options);
        var validator = new PlaceHoldCommandValidator(options);
        var actor = Guid.NewGuid();

        validator.Validate(new PlaceHoldCommand(actor, [], "legal", "dispute")).IsValid
            .Should().BeFalse("an empty batch is meaningless");
        validator.Validate(new PlaceHoldCommand(actor, [Guid.NewGuid()], "temporary", "dispute")).IsValid
            .Should().BeFalse("holdType must be legal or administrative");
        validator.Validate(new PlaceHoldCommand(actor, [Guid.NewGuid()], "legal", "because-i-said-so")).IsValid
            .Should().BeFalse("free-text reasons never enter the journal");
        validator.Validate(new PlaceHoldCommand(actor, new[] { Guid.NewGuid(), Guid.NewGuid() }, "administrative", "other")).IsValid
            .Should().BeTrue();
    }

    [Fact]
    public void Export_rejects_unknown_formats_and_out_of_cap_row_counts()
    {
        var validator = new ExportAuditCommandValidator(Microsoft.Extensions.Options.Options.Create(Options));
        var actor = Guid.NewGuid();
        var filters = new AuditQueryFilters(null, null, null, null, null, null, null, null, null, null);

        validator.Validate(new ExportAuditCommand(actor, "xlsx", filters, false, null)).IsValid.Should().BeFalse();
        validator.Validate(new ExportAuditCommand(actor, "csv", filters, false, Options.ExportMaxRows + 1)).IsValid
            .Should().BeFalse();
        validator.Validate(new ExportAuditCommand(actor, "ndjson", filters, true, 100)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Purge_accepts_zero_as_use_the_default()
    {
        var validator = new PurgeExpiredCommandValidator();

        validator.Validate(new PurgeExpiredCommand(Guid.NewGuid(), 0)).IsValid.Should().BeTrue();
        validator.Validate(new PurgeExpiredCommand(Guid.NewGuid(), -1)).IsValid.Should().BeFalse();
    }
}
