using CommunityOS.Localization.Domain;
using CommunityOS.Localization.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace CommunityOS.Localization.Tests.Domain;

/// <summary>Ratified domain invariants (ADR-029): locale lifecycle, revision
/// immutability with verbatim supersession, reserved source contexts and the
/// suggestion accept-into-review boundary.</summary>
public sealed class LocalizationDomainTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    // ---- BCP-47 ----

    [Theory]
    [InlineData("en", true)]
    [InlineData("fa-IR", true)]
    [InlineData("zh-Hans-CN", true)]
    [InlineData("x-private", false)]
    [InlineData("en-", false)]
    [InlineData("-en", false)]
    [InlineData("e", false)]
    [InlineData("", false)]
    public void Bcp47_validation_matches_pragmatic_rules(string code, bool expected) =>
        Bcp47.IsValid(code).Should().Be(expected);

    [Fact]
    public void Bcp47_normalize_is_lowercase()
    {
        Bcp47.Normalize("fa-IR").Should().Be("fa-ir");
        Bcp47.Normalize("EN").Should().Be("en");
    }

    [Theory]
    [InlineData("fa-IR", new[] { "fa-ir", "fa" })]
    [InlineData("en", new[] { "en" })]
    public void Fallback_chain_walks_parent_subtags(string culture, string[] expected)
    {
        var chain = Bcp47.FallbackChain(culture);
        chain.Should().Equal(expected);
    }

    // ---- Locale lifecycle ----

    [Fact]
    public void Register_starts_registered_and_not_default()
    {
        var locale = Locale.Register("fr", "French", Actor, Now);
        locale.Status.Should().Be(LocaleStatus.Registered);
        locale.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_rejects_the_default_locale()
    {
        var locale = Locale.CreateDefaultSeed(Guid.NewGuid(), "en", "English", Actor, Now);
        var act = () => locale.Deactivate(Now);
        act.Should().Throw<LocalizationConflictException>();
    }

    [Fact]
    public void Registering_an_invalid_code_throws()
    {
        var act = () => Locale.Register("not a code!", null, Actor, Now);
        act.Should().Throw<ArgumentException>();
    }

    // ---- Resource entry lifecycle ----

    [Fact]
    public void Approve_supersedes_the_previous_approved_value_verbatim()
    {
        var nsId = Guid.NewGuid();
        var entry = ResourceEntry.Create(nsId, "common.greeting", Now);
        var v1 = entry.ProposeDraft("fa", "سلام", ContentProvenance.ForHuman(), Actor, Now);
        entry.SubmitForReview(v1.Id, Now);
        entry.Approve(v1.Id, Guid.NewGuid(), Now);

        var v2 = entry.ProposeDraft("FA", "درود", ContentProvenance.ForHuman(), Actor, Now);
        entry.SubmitForReview(v2.Id, Now);
        entry.Approve(v2.Id, Guid.NewGuid(), Now);

        v1.State.Should().Be(ReviewState.Superseded);
        v1.Value.Should().Be("سلام");
        v2.State.Should().Be(ReviewState.Approved);
        entry.CurrentApproved("fa")!.Value.Should().Be("درود");
    }

    [Fact]
    public void Illegal_transitions_are_conflicts()
    {
        var entry = ResourceEntry.Create(Guid.NewGuid(), "k", Now);
        var r = entry.ProposeDraft("de", "Wert", ContentProvenance.ForHuman(), Actor, Now);

        var act = () => entry.Approve(r.Id, Guid.NewGuid(), Now);
        act.Should().Throw<LocalizationConflictException>();
    }

    [Fact]
    public void Deprecated_keys_accept_no_new_revisions()
    {
        var entry = ResourceEntry.Create(Guid.NewGuid(), "old.key", Now);
        entry.Deprecate(Now);

        var act = () => entry.ProposeDraft("en", "v", ContentProvenance.ForHuman(), Actor, Now);
        act.Should().Throw<LocalizationConflictException>();
    }

    [Fact]
    public void Keys_restrict_charset_and_length()
    {
        var act = () => ResourceEntry.Create(Guid.NewGuid(), "bad key!", Now);
        act.Should().Throw<ArgumentException>();

        var tooLong = new string('a', 201);
        var act2 = () => ResourceEntry.Create(Guid.NewGuid(), tooLong, Now);
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Values_are_bounded()
    {
        var entry = ResourceEntry.Create(Guid.NewGuid(), "k", Now);
        var tooLong = new string('x', ResourceRevision.ValueMaxLength + 1);
        var act = () => entry.ProposeDraft("en", tooLong, ContentProvenance.ForHuman(), Actor, Now);
        act.Should().Throw<ArgumentException>();
    }

    // ---- Entity translations ----

    [Fact]
    public void Reserved_source_contexts_are_rejected()
    {
        var entityId = Guid.NewGuid();
        var actLibrary = () => EntityTranslation.Create(
            "library", "document", entityId, "display_name", "en", Now);
        var actKnowledge = () => EntityTranslation.Create(
            "knowledge", "article", entityId, "title", "en", Now);

        actLibrary.Should().Throw<LocalizationConflictException>();
        actKnowledge.Should().Throw<LocalizationConflictException>();
    }

    [Fact]
    public async Task Namespace_creation_rejects_reserved_prefixes()
    {
        var act = () => ResourceNamespace.Create("library.ui", null, Actor, Now);
        act.Should().Throw<LocalizationConflictException>();
    }

    [Fact]
    public void Entity_translation_lifecycle_mirrors_resources()
    {
        var t = EntityTranslation.Create(
            "community", "person", Guid.NewGuid(), "display_name", "en", Now);
        var r1 = t.ProposeDraft("Alice", ContentProvenance.ForHuman(), Actor, Now);
        t.SubmitForReview(r1.Id, Now);
        t.Approve(r1.Id, Guid.NewGuid(), Now);

        var r2 = t.ProposeDraft("Bob", ContentProvenance.ForHuman(), Actor, Now);
        t.SubmitForReview(r2.Id, Now);
        t.Reject(r2.Id, Guid.NewGuid(), Now);

        t.CurrentApproved()!.Value.Should().Be("Alice");
        r2.State.Should().Be(ReviewState.Rejected);
    }

    // ---- Suggestions ----

    [Fact]
    public void Suggestion_accept_records_decision_once()
    {
        var suggestion = TranslationSuggestion.Create(
            "resource_entry", Guid.NewGuid(), "fa", "مقدار",
            ContentProvenance.ForMachine("azure-translator"), Now);

        suggestion.AcceptIntoReview(Guid.NewGuid(), Now);
        var act = () => suggestion.AcceptIntoReview(Guid.NewGuid(), Now);
        act.Should().Throw<LocalizationConflictException>();
    }

    [Fact]
    public void Machine_provenance_round_trips_through_storage()
    {
        var provenance = ContentProvenance.FromStored(
            ContentProvenance.ForMachine("Azure-Translator").ToString());
        provenance.IsMachine.Should().BeTrue();

        var human = ContentProvenance.FromStored("human");
        human.IsMachine.Should().BeFalse();

        var act = () => ContentProvenance.FromStored("alien");
        act.Should().Throw<ArgumentException>();
    }
}
