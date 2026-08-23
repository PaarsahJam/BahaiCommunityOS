using System.Globalization;
using CommunityOS.Correspondence.API.Controllers;
using CommunityOS.Correspondence.Application;
using CommunityOS.Correspondence.Infrastructure.Retention;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CommunityOS.Correspondence.Tests.Infrastructure;

public sealed class CorrespondenceRetentionPolicyTests
{
    [Fact]
    public void Category_overrides_map_to_classes_and_calendar_durations()
    {
        var policy = new CorrespondenceRetentionPolicy(Options.Create(new CorrespondenceOptions
        {
            Retention = new()
            {
                DefaultClass = "default",
                Classes = new Dictionary<string, string?> { ["default"] = null, ["administrative-2y"] = "P2Y" },
                EventClasses = new Dictionary<string, string> { ["admin-memo"] = "administrative-2y" }
            }
        }));

        var submitted = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

        var mapped = policy.Assign("admin-memo", submitted);
        mapped.Class.Should().Be("administrative-2y");
        mapped.ExpiresOn.Should().Be(new DateTime(2028, 3, 15, 12, 0, 0, DateTimeKind.Utc));

        // Unknown categories retain indefinitely under the default class.
        var unmapped = policy.Assign("general", submitted);
        unmapped.Class.Should().Be("default");
        unmapped.ExpiresOn.Should().BeNull("expiry alone never deletes; indefinite by default");
    }

    [Fact]
    public void Malformed_configured_durations_fail_fast_at_startup()
    {
        var create = () => new CorrespondenceRetentionPolicy(Options.Create(new CorrespondenceOptions
        {
            Retention = new()
            {
                Classes = new Dictionary<string, string?> { ["broken"] = "7-years" }
            }
        }));

        create.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Composite_durations_use_calendar_arithmetic()
    {
        var policy = new CorrespondenceRetentionPolicy(Options.Create(new CorrespondenceOptions
        {
            Retention = new()
            {
                DefaultClass = "mixed",
                Classes = new Dictionary<string, string?> { ["mixed"] = "P1Y2M10D" }
            }
        }));

        var assignment = policy.Assign("anything", new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc));
        assignment.ExpiresOn.Should().Be(new DateTime(2027, 4, 10, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Csv_fields_are_quoted_per_rfc_4180()
    {
        LettersController.CsvField(null).Should().BeEmpty();
        LettersController.CsvField("plain").Should().Be("plain");
        LettersController.CsvField("has,comma").Should().Be("\"has,comma\"");
        LettersController.CsvField("say \"hi\"").Should().Be("\"say \"\"hi\"\"\"");
        LettersController.CsvField("line\nbreak").Should().Be("\"line\nbreak\"");
        CultureInfo.CurrentCulture.Should().NotBeNull();
    }
}
