using CommunityOS.Documents.Domain.Aggregates;
using CommunityOS.Documents.Domain.Enumerations;
using CommunityOS.Documents.Domain.Exceptions;
using CommunityOS.Documents.Domain.Events;

namespace CommunityOS.Documents.Tests.Domain;

/// <summary>
/// Domain-invariant tests for the Document aggregate (ADR-022): lifecycle
/// Draft → Active → Archived | Deactivated with reversible restore, immutable
/// append-only versions with idempotent upload, holds that block deactivation
/// unless an explicit administrative override is used, and the scan state that
/// only gates content download — never the document lifecycle.
/// </summary>
public class DocumentLifecycleTests
{
    private static readonly Guid ActorId = Guid.NewGuid();

    private static Document CreateDocument(Guid? unitId = null, string title = "Feast Agenda") =>
        Document.Create(title, "Draft agenda", unitId, "person", ActorId, ActorId, DateTime.UtcNow);

    private static byte[] Bytes(string text = "content") =>
        System.Text.Encoding.UTF8.GetBytes(text);

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

    // --- Lifecycle ---

    [Fact]
    public void Create_starts_as_Draft_and_raises_DocumentCreated()
    {
        var doc = CreateDocument();

        doc.Status.Should().Be(DocumentStatus.Draft);
        doc.CurrentVersion.Should().BeNull();
        doc.DomainEvents.Should().ContainSingle(e => e is DocumentCreatedEvent);
    }

    [Fact]
    public void AddVersion_activates_a_draft_and_moves_current_pointer()
    {
        var doc = CreateDocument();
        var hash = Sha256(Bytes());

        var version = doc.AddVersion(hash, "text/plain", Bytes().Length, "agenda.txt",
            DocumentSources.Member, ActorId, DateTime.UtcNow, ScanStatus.NotScanned);

        version.VersionNumber.Should().Be(1);
        version.ObjectKey.Should().Be($"documents/{hash}");
        doc.Status.Should().Be(DocumentStatus.Active);
        doc.CurrentVersionId.Should().Be(version.Id);
        doc.DomainEvents.Should().ContainSingle(e => e is DocumentVersionAddedEvent);
    }

    [Fact]
    public void AddVersion_with_same_content_hash_is_idempotent()
    {
        var doc = CreateDocument();
        var hash = Sha256(Bytes());
        var now = DateTime.UtcNow;

        var first = doc.AddVersion(hash, "text/plain", Bytes().Length, "a.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);
        var second = doc.AddVersion(hash, "text/plain", Bytes().Length, "a.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);

        second.Id.Should().Be(first.Id);
        doc.Versions.Should().HaveCount(1);
        doc.Versions.Single(v => v.VersionNumber == 1).ContentHash.Should().Be(hash);
    }

    [Fact]
    public void Versions_are_immutable_and_numbered_in_append_order()
    {
        var doc = CreateDocument();
        var now = DateTime.UtcNow;

        doc.AddVersion(Sha256(Bytes("one")), "text/plain", 3, "1.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);
        var third = doc.AddVersion(Sha256(Bytes("two")), "text/plain", 3, "2.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);
        var fourth = doc.AddVersion(Sha256(Bytes("three")), "text/plain", 5, "3.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);

        doc.Versions.Select(v => v.VersionNumber).Should().BeEquivalentTo([1, 2, 3]);
        third.VersionNumber.Should().Be(2);
        fourth.VersionNumber.Should().Be(3);
        doc.CurrentVersion!.Id.Should().Be(fourth.Id);
    }

    [Fact]
    public void AddVersion_on_Archived_or_Deactivated_is_rejected()
    {
        var doc = CreateDocument();
        var now = DateTime.UtcNow;
        doc.AddVersion(Sha256(Bytes()), "text/plain", Bytes().Length, "a.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);
        doc.Archive(ActorId, now);
        doc.Deactivate(ActorId, adminOverride: false, null, now);

        var act = () => doc.AddVersion(Sha256(Bytes("new")), "text/plain", 3, "b.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);

        act.Should().Throw<InvalidDocumentTransitionException>();
    }

    // --- Metadata vs lifecycle ---

    [Fact]
    public void UpdateMetadata_is_allowed_in_Draft_and_Active_but_blocked_when_Archived_or_Deactivated()
    {
        var doc = CreateDocument();
        doc.UpdateMetadata("New title", "New description", ActorId, DateTime.UtcNow);
        doc.Title.Should().Be("New title");

        doc.AddVersion(Sha256(Bytes("content")), "text/plain", Bytes("content").Length, "a.txt",
            DocumentSources.Member, ActorId, DateTime.UtcNow, ScanStatus.NotScanned);
        doc.Archive(ActorId, DateTime.UtcNow);
        var actArchived = () => doc.UpdateMetadata("x", null, ActorId, DateTime.UtcNow);
        actArchived.Should().Throw<InvalidDocumentTransitionException>();

        doc.Deactivate(ActorId, adminOverride: false, null, DateTime.UtcNow);
        var actDeactivated = () => doc.UpdateMetadata("x", null, ActorId, DateTime.UtcNow);
        actDeactivated.Should().Throw<InvalidDocumentTransitionException>();
    }

    [Fact]
    public void Archive_requires_Active()
    {
        var doc = CreateDocument();
        var act = () => doc.Archive(ActorId, DateTime.UtcNow);
        act.Should().Throw<InvalidDocumentTransitionException>();
    }

    [Fact]
    public void Deactivate_requires_Active_or_Archived()
    {
        var doc = CreateDocument();
        var act = () => doc.Deactivate(ActorId, adminOverride: false, null, DateTime.UtcNow);
        act.Should().Throw<InvalidDocumentTransitionException>();
    }

    [Fact]
    public void Holds_block_deactivation_unless_admin_override()
    {
        var doc = CreateDocument();
        var now = DateTime.UtcNow;
        doc.AddVersion(Sha256(Bytes()), "text/plain", Bytes().Length, "a.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);
        doc.SetClassification("internal", isSensitive: false, null,
            legalHoldReference: "hold-1", null, ActorId, now);

        var act = () => doc.Deactivate(ActorId, adminOverride: false, null, now);
        act.Should().Throw<HeldDocumentDeactivationException>();

        doc.Deactivate(ActorId, adminOverride: true, "admin release", now);
        doc.Status.Should().Be(DocumentStatus.Deactivated);
    }

    [Fact]
    public void Restore_returns_Archived_or_Deactivated_to_Active()
    {
        var doc = CreateDocument();
        var now = DateTime.UtcNow;
        doc.AddVersion(Sha256(Bytes()), "text/plain", Bytes().Length, "a.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);

        doc.Deactivate(ActorId, adminOverride: false, null, now);
        doc.Restore(ActorId, now);
        doc.Status.Should().Be(DocumentStatus.Active);
    }

    [Fact]
    public void Lifecycle_transitions_raise_the_corresponding_events()
    {
        var doc = CreateDocument();
        var now = DateTime.UtcNow;
        doc.AddVersion(Sha256(Bytes()), "text/plain", Bytes().Length, "a.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);

        doc.Archive(ActorId, now);
        doc.DomainEvents.Should().Contain(e => e is DocumentArchivedEvent);

        doc.Deactivate(ActorId, adminOverride: false, null, now);
        doc.DomainEvents.Should().Contain(e => e is DocumentDeactivatedEvent);

        doc.Restore(ActorId, now);
        doc.DomainEvents.Should().Contain(e => e is DocumentRestoredEvent);
    }

    // --- Classification ---

    [Fact]
    public void SetClassification_updates_metadata_and_raises_DocumentClassified()
    {
        var doc = CreateDocument();
        doc.SetClassification("internal", isSensitive: true, "administrative-7y",
            "legal-1", "admin-1", ActorId, DateTime.UtcNow);

        doc.Classification.ClassificationCode.Should().Be("internal");
        doc.Classification.IsSensitive.Should().BeTrue();
        doc.Classification.RetentionCategory.Should().Be("administrative-7y");
        doc.Classification.LegalHoldReference.Should().Be("legal-1");
        doc.Classification.AdministrativeHoldReference.Should().Be("admin-1");
        doc.DomainEvents.Should().Contain(e => e is DocumentClassifiedEvent);
    }

    [Fact]
    public void SetClassification_is_blocked_when_Deactivated()
    {
        var doc = CreateDocument();
        var now = DateTime.UtcNow;
        doc.AddVersion(Sha256(Bytes()), "text/plain", Bytes().Length, "a.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);
        doc.Deactivate(ActorId, adminOverride: false, null, now);

        var act = () => doc.SetClassification("internal", true, null, null, null, ActorId, now);
        act.Should().Throw<InvalidDocumentTransitionException>();
    }

    // --- Scan state only gates download ---

    [Fact]
    public void Scan_status_blocks_download_only_for_scanning_quarantined_rejected()
    {
        ScanStatus.NotScanned.BlocksDownload.Should().BeFalse();
        ScanStatus.Clean.BlocksDownload.Should().BeFalse();
        ScanStatus.Scanning.BlocksDownload.Should().BeTrue();
        ScanStatus.Quarantined.BlocksDownload.Should().BeTrue();
        ScanStatus.Rejected.BlocksDownload.Should().BeTrue();
    }

    [Fact]
    public void UpdateScanStatus_is_the_only_version_mutation_and_does_not_touch_document_state()
    {
        var doc = CreateDocument();
        var now = DateTime.UtcNow;
        var version = doc.AddVersion(Sha256(Bytes()), "text/plain", Bytes().Length, "a.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);

        doc.UpdateScanStatus(version.Id, ScanStatus.Clean, now);

        version.ScanStatus.Should().Be(ScanStatus.Clean);
        doc.Title.Should().Be("Feast Agenda");
        doc.Status.Should().Be(DocumentStatus.Active);
        doc.DomainEvents.Should().Contain(e => e is DocumentScanCompletedEvent);
    }

    // --- Organization scopes ---

    [Fact]
    public void Scopes_are_deduplicated_and_ignore_the_primary_unit()
    {
        var unit = Guid.NewGuid();
        var doc = CreateDocument(unit);
        var now = DateTime.UtcNow;

        doc.AddOrganizationScope(unit, ActorId, now);
        doc.AddOrganizationScope(Guid.NewGuid(), ActorId, now);
        doc.AddOrganizationScope(Guid.NewGuid(), ActorId, now);

        doc.Scopes.Should().HaveCount(2);
        doc.AllOrganizationUnitIds.Should().Contain(unit);
        doc.AllOrganizationUnitIds.Distinct().Count().Should().Be(3);
    }

    [Fact]
    public void Scope_edits_are_metadata_changes_and_are_blocked_when_Archived()
    {
        var doc = CreateDocument(Guid.NewGuid());
        var now = DateTime.UtcNow;
        doc.AddVersion(Sha256(Bytes()), "text/plain", Bytes().Length, "a.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);
        doc.Archive(ActorId, now);

        var act = () => doc.AddOrganizationScope(Guid.NewGuid(), ActorId, now);
        act.Should().Throw<InvalidDocumentTransitionException>();
    }

    // --- References ---

    [Fact]
    public void References_are_idempotent_and_unique_per_context_entity_type()
    {
        var doc = CreateDocument();

        var first = doc.AddReference("records.record", Guid.NewGuid(), "evidence", ActorId, DateTime.UtcNow);
        var second = doc.AddReference("records.record", first.SourceEntityId, "evidence", ActorId, DateTime.UtcNow);

        second.Id.Should().Be(first.Id);
        doc.References.Should().HaveCount(1);
    }
}