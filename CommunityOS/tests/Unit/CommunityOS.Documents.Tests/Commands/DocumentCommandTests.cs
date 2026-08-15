using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Documents.Application;
using CommunityOS.Documents.Application.Abstractions;
using CommunityOS.Documents.Application.Commands;
using CommunityOS.Documents.Application.Options;
using CommunityOS.Documents.Application.Queries;
using CommunityOS.Documents.Domain.Aggregates;
using CommunityOS.Documents.Domain.Enumerations;
using CommunityOS.Documents.Domain.Exceptions;
using CommunityOS.Documents.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CommunityOS.Documents.Tests.Commands;

/// <summary>
/// Positive command tests for the content pipeline (ADR-022, ratified):
/// SHA-256 content addressing on upload, idempotent re-upload of identical
/// content, size and MIME allowlist enforcement, and SHA-256 verification on
/// download (content-integrity owner).
/// </summary>
public class DocumentCommandTests
{
    private static readonly Guid ActorId = Guid.NewGuid();

    private sealed class Harness
    {
        public RepoSet Repos { get; } = RepoSet.Create();
        public ServiceProvider Provider { get; }
        public bool VerifyHashOnRead { get; init; }

        public Harness()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
            services.AddDocumentsApplication();

            services.AddScoped(_ => Repos.Documents);
            services.AddScoped(_ => Repos.Units);
            services.AddScoped(_ => Repos.Storage);
            services.AddScoped(_ => Repos.Scanner);
            services.AddScoped(_ => Repos.Evaluator);
            services.AddSingleton<IOptions<DocumentsIntegrityOptions>>(_ =>
                new OptionsWrapper<DocumentsIntegrityOptions>(new DocumentsIntegrityOptions { VerifyHashOnRead = VerifyHashOnRead }));

            Provider = services.BuildServiceProvider();
        }

        public ISender Sender => Provider.GetRequiredService<ISender>();

        public void AllowAll() => Repos.Evaluator.EvaluateAsync(
                Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => AuthorizationDecision.Allow(
                "allow", [call.Arg<AuthorizationRequest>().Permission], DateTime.UtcNow));
    }

    private sealed record RepoSet(
        IDocumentRepository Documents,
        IOrganizationUnitReferenceRepository Units,
        IDocumentObjectStorage Storage,
        IDocumentScanService Scanner,
        IAuthorizationEvaluator Evaluator)
    {
        public static RepoSet Create() => new(
            Substitute.For<IDocumentRepository>(),
            Substitute.For<IOrganizationUnitReferenceRepository>(),
            Substitute.For<IDocumentObjectStorage>(),
            Substitute.For<IDocumentScanService>(),
            Substitute.For<IAuthorizationEvaluator>());
    }

    private static byte[] ContentBytes(string text = "content") =>
        System.Text.Encoding.UTF8.GetBytes(text);

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

    private static Document StubDocument(Guid? unitId) =>
        Document.Create("Feast Agenda", "Draft", unitId, "person", ActorId, ActorId, DateTime.UtcNow);

    // --- Upload: content addressing ---

    [Fact]
    public async Task Upload_stores_content_under_the_content_addressed_key_and_activates_draft()
    {
        var h = new Harness { VerifyHashOnRead = false };
        h.AllowAll();
        var unit = Guid.NewGuid();
        var doc = StubDocument(unit);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);
        var bytes = ContentBytes();
        h.Repos.Storage.ExistsAsync($"documents/{Sha256(bytes)}", Arg.Any<CancellationToken>()).Returns(false);
        h.Repos.Scanner.ScanAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(ScanStatus.NotScanned);

        var version = await h.Sender.Send(new UploadDocumentVersionCommand(
            ActorId, doc.Id, new MemoryStream(bytes), bytes.Length, "agenda.txt",
            "text/plain", 50 * 1024 * 1024, ["text/plain"]));

        version.ContentHash.Should().Be(Sha256(bytes));
        version.VersionNumber.Should().Be(1);
        doc.Status.Should().Be(DocumentStatus.Active);
        doc.CurrentVersion!.ObjectKey.Should().Be($"documents/{Sha256(bytes)}");

        await h.Repos.Storage.Received(1).PutAsync(
            $"documents/{Sha256(bytes)}", Arg.Any<Stream>(), "text/plain", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_of_identical_content_is_idempotent_and_skips_storage()
    {
        var h = new Harness { VerifyHashOnRead = false };
        h.AllowAll();
        var unit = Guid.NewGuid();
        var doc = StubDocument(unit);
        var now = DateTime.UtcNow;
        var bytes = ContentBytes();
        doc.AddVersion(Sha256(bytes), "text/plain", bytes.Length, "agenda.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);

        var version = await h.Sender.Send(new UploadDocumentVersionCommand(
            ActorId, doc.Id, new MemoryStream(bytes), bytes.Length, "agenda.txt",
            "text/plain", 50 * 1024 * 1024, ["text/plain"]));

        version.VersionNumber.Should().Be(1);
        doc.Versions.Should().HaveCount(1);
        await h.Repos.Storage.DidNotReceiveWithAnyArgs().PutAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Upload_exceeding_max_size_throws_413_and_never_persists()
    {
        var h = new Harness { VerifyHashOnRead = false };
        h.AllowAll();
        var unit = Guid.NewGuid();
        var doc = StubDocument(unit);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);

        var act = () => h.Sender.Send(new UploadDocumentVersionCommand(
            ActorId, doc.Id, new MemoryStream(ContentBytes()), ContentBytes().Length, "a.txt",
            "text/plain", MaxFileSizeBytes: 1, ["text/plain"]));

        await act.Should().ThrowAsync<DocumentContentTooLargeException>();
        await h.Repos.Documents.DidNotReceive().UpdateAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_with_disallowed_mime_type_throws_415()
    {
        var h = new Harness { VerifyHashOnRead = false };
        h.AllowAll();
        var unit = Guid.NewGuid();
        var doc = StubDocument(unit);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);

        var act = () => h.Sender.Send(new UploadDocumentVersionCommand(
            ActorId, doc.Id, new MemoryStream(ContentBytes()), ContentBytes().Length, "evil.exe",
            "application/x-msdownload", 50 * 1024 * 1024, ["text/plain"]));

        await act.Should().ThrowAsync<UnsupportedDocumentMimeTypeException>();
        await h.Repos.Documents.DidNotReceive().UpdateAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>());
    }

    // --- Download: SHA-256 verification on read ---

    [Fact]
    public async Task Download_verifies_sha256_and_streams_when_hash_matches()
    {
        var h = new Harness { VerifyHashOnRead = true };
        h.AllowAll();
        var unit = Guid.NewGuid();
        var doc = StubDocument(unit);
        var now = DateTime.UtcNow;
        var bytes = ContentBytes();
        doc.AddVersion(Sha256(bytes), "text/plain", bytes.Length, "agenda.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);
        h.Repos.Storage.GetAsync($"documents/{Sha256(bytes)}", Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(bytes));

        var content = await h.Sender.Send(new DownloadDocumentVersionQuery(ActorId, doc.Id, null));

        using var reader = new StreamReader(content.Content);
        reader.ReadToEnd().Should().Be("content");
    }

    [Fact]
    public async Task Download_rejects_corrupted_content_when_hash_mismatches()
    {
        var h = new Harness { VerifyHashOnRead = true };
        h.AllowAll();
        var unit = Guid.NewGuid();
        var doc = StubDocument(unit);
        var now = DateTime.UtcNow;
        var bytes = ContentBytes();
        doc.AddVersion(Sha256(bytes), "text/plain", bytes.Length, "agenda.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);
        // Storage returns tampered bytes (different content, same stream).
        h.Repos.Storage.GetAsync($"documents/{Sha256(bytes)}", Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(ContentBytes("tampered")));

        var act = () => h.Sender.Send(new DownloadDocumentVersionQuery(ActorId, doc.Id, null));

        await act.Should().ThrowAsync<DocumentIntegrityViolationException>();
    }

[Fact]
    public async Task Download_of_a_scan_blocking_version_is_forbidden()
    {
        var h = new Harness { VerifyHashOnRead = false };
        h.AllowAll();
        var unit = Guid.NewGuid();
        var doc = StubDocument(unit);
        var now = DateTime.UtcNow;
        var bytes = ContentBytes();
        doc.AddVersion(Sha256(bytes), "text/plain", bytes.Length, "a.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.Quarantined);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);

        var act = () => h.Sender.Send(new DownloadDocumentVersionQuery(ActorId, doc.Id, null));

        await act.Should().ThrowAsync<ContentNotDownloadableException>();
        await h.Repos.Storage.DidNotReceiveWithAnyArgs().GetAsync(default!, default);
    }

    // --- M-1 regression: person-owned documents (OrganizationUnitId == null) ---

    [Fact]
    public async Task Create_person_owned_document_maps_to_dto_without_throwing()
    {
        var h = new Harness { VerifyHashOnRead = false };
        h.AllowAll();

        var dto = await h.Sender.Send(new CreateDocumentCommand(
            ActorId, "Personal Notes", null, "person", ActorId, OrganizationUnitId: null, IsSensitive: false));

        dto.Id.Should().NotBe(Guid.Empty);
        dto.OrganizationUnitId.Should().BeNull();
        dto.OrganizationScopes.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_person_owned_document_maps_to_dto_and_keeps_its_scopes()
    {
        var h = new Harness { VerifyHashOnRead = false };
        h.AllowAll();
        var scopeA = Guid.NewGuid();
        var scopeB = Guid.NewGuid();
        var doc = Document.Create("Personal Notes", null, organizationUnitId: null, "person", ActorId, ActorId, DateTime.UtcNow);
        doc.AddOrganizationScope(scopeA, ActorId, DateTime.UtcNow);
        doc.AddOrganizationScope(scopeB, ActorId, DateTime.UtcNow);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);

        var dto = await h.Sender.Send(new GetDocumentQuery(ActorId, doc.Id));

        dto.OrganizationUnitId.Should().BeNull();
        dto.OrganizationScopes.Should().BeEquivalentTo(new[] { scopeA, scopeB });
    }

    [Fact]
    public async Task Get_organization_owned_document_excludes_the_primary_unit_from_scopes()
    {
        var h = new Harness { VerifyHashOnRead = false };
        h.AllowAll();
        var primary = Guid.NewGuid();
        var extra = Guid.NewGuid();
        var doc = Document.Create("Feast Agenda", "Draft", primary, "person", ActorId, ActorId, DateTime.UtcNow);
        doc.AddOrganizationScope(extra, ActorId, DateTime.UtcNow);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);

        var dto = await h.Sender.Send(new GetDocumentQuery(ActorId, doc.Id));

        dto.OrganizationUnitId.Should().Be(primary);
        dto.OrganizationScopes.Should().BeEquivalentTo(new[] { extra });
    }
}

