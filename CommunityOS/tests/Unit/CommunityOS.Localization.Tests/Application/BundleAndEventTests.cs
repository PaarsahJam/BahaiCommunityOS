using CommunityOS.Localization.Application;
using CommunityOS.Contracts.Localization;
using CommunityOS.Localization.Domain.Events;
using FluentAssertions;
using System.Reflection;
using Xunit;

namespace CommunityOS.Localization.Tests.Application;

public sealed class BundleResolverTests
{
    private static ExportCandidateRow Candidate(
        string ns, string key, params (string Culture, string Value)[] values)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (culture, value) in values)
        {
            dict[culture] = value;
        }

        return new ExportCandidateRow(Guid.NewGuid(), ns, key, dict);
    }

    [Fact]
    public void Fallback_walks_region_to_language_then_default()
    {
        var candidates = new List<ExportCandidateRow>
        {
            Candidate("ui", "greeting", ("fa", "سلام")),
            Candidate("ui", "farewell", ("en", "Goodbye"))
        };

        var items = BundleResolver.Resolve(candidates, "fa-IR", "en");

        items.Should().HaveCount(2);
        items[0].Value.Should().Be("سلام");
        items[0].ResolvedFrom.Should().Be("fa");
        items[1].Value.Should().Be("Goodbye");
        items[1].ResolvedFrom.Should().Be("en");
    }

    [Fact]
    public void Unresolved_keys_are_omitted_fail_visible()
    {
        var candidates = new List<ExportCandidateRow>
        {
            Candidate("ui", "only-de", ("de", "Hallo"))
        };

        var items = BundleResolver.Resolve(candidates, "fa", "en");

        items.Should().BeEmpty();
    }

    [Fact]
    public void Requested_culture_beats_fallbacks()
    {
        var candidates = new List<ExportCandidateRow>
        {
            Candidate("ui", "greeting",
                ("fa-ir", "درود"), ("fa", "سلام"), ("en", "Hello"))
        };

        var items = BundleResolver.Resolve(candidates, "fa-IR", "en");

        items.Single().Value.Should().Be("درود");
        items.Single().ResolvedFrom.Should().Be("fa-ir");
    }

    [Fact]
    public void Etags_are_deterministic_per_version()
    {
        BundleResolver.ETagFor(7).Should().Be(BundleResolver.ETagFor(7));
        BundleResolver.ETagFor(7).Should().NotBe(BundleResolver.ETagFor(8));
    }
}

/// <summary>
/// Payload allowlist (mirrors the Correspondence gate): the ratified catalog
/// fact carries codes, a version and a timestamp only — never translation
/// values or any other catalog content.
/// </summary>
public sealed class CatalogEventPayloadAllowlistTests
{
    [Fact]
    public void Integration_contract_properties_are_exactly_the_allowlist()
    {
        var properties = typeof(LocalizationCatalogChanged)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        properties.Should().Equal([
            "BundleVersion", "CatalogContext", "Culture", "Namespace", "OccurredOn"
        ]);
    }

    [Fact]
    public void Domain_event_carries_no_catalog_content()
    {
        var domainProperties = typeof(CatalogChangedDomainEvent)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        domainProperties.Should().Equal(["BundleVersion", "CatalogContext", "Culture", "Namespace"]);
    }
}
