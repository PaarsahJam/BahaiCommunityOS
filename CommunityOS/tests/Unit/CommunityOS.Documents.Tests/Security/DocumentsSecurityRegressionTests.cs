using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Contracts.Documents;
using CommunityOS.Documents.Application;
using CommunityOS.Documents.Application.Abstractions;
using CommunityOS.Documents.Application.Commands;
using CommunityOS.Documents.Application.Options;
using CommunityOS.Documents.Application.Permissions;
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

namespace CommunityOS.Documents.Tests.Security;

/// <summary>
/// Security regression tests. These lock in the authorization boundaries of the
/// Documents bounded context (ADR-022): every command and guarded query flows
/// through the Authorization guard, the guard is fail-closed (a denied or
/// unreachable evaluator blocks the operation and nothing is persisted and no
/// storage side effect occurs), metadata and content are separate capabilities,
/// sensitive content requires an extra permission, and denied reads are
/// indistinguishable from not-found (no enumeration oracle).
/// </summary>
public class DocumentsSecurityRegressionTests
{
    private static readonly Guid ActorId = Guid.NewGuid();

    private sealed class Harness
    {
        public RepoSet Repos { get; } = RepoSet.Create();
        public ServiceProvider Provider { get; }

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
                new OptionsWrapper<DocumentsIntegrityOptions>(new DocumentsIntegrityOptions()));

            Provider = services.BuildServiceProvider();
        }

        public ISender Sender => Provider.GetRequiredService<ISender>();

        /// <summary>Denies every authorization request (fail-closed default).</summary>
        public void DenyAll() => Repos.Evaluator.EvaluateAsync(
                Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.NoPermission, DateTime.UtcNow));

/// <summary>Allows a single permission, optionally scoped to a resource.</summary>
        public void Allow(string permission, Guid? resourceId = null, Guid? unitId = null) =>
            Repos.Evaluator.EvaluateAsync(
                    Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var r = call.Arg<AuthorizationRequest>();
                    if (r.Permission != permission) return Deny();
                    if (resourceId is { } id && r.Context.ResourceId != id) return Deny();
                    if (unitId is { } u && r.Context.OrganizationUnitId != u) return Deny();
                    return AuthorizationDecision.Allow("allow", [r.Permission], DateTime.UtcNow);
                });

        /// <summary>Allows any of several permissions, optionally scoped to a resource.</summary>
        public void AllowAny(IReadOnlyCollection<string> permissions, Guid? resourceId = null, Guid? unitId = null) =>
            Repos.Evaluator.EvaluateAsync(
                    Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var r = call.Arg<AuthorizationRequest>();
                    if (!permissions.Contains(r.Permission)) return Deny();
                    if (resourceId is { } id && r.Context.ResourceId != id) return Deny();
                    if (unitId is { } u && r.Context.OrganizationUnitId != u) return Deny();
                    return AuthorizationDecision.Allow("allow", [r.Permission], DateTime.UtcNow);
                });

        /// <summary>
        /// Fail-closed batch evaluation used by list filtering: the coarse
        /// scope-level read (resourceId is null) is granted, but every
        /// per-document batch check is allowed only for the listed resources.
        /// </summary>
        public void AllowBatchOnly(params Guid[] allowedResourceIds)
        {
            Repos.Evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var r = call.Arg<AuthorizationRequest>();
                    return (r.Context.ResourceId is null ||
                            allowedResourceIds.Contains(r.Context.ResourceId.Value))
                        ? AuthorizationDecision.Allow("allow", [r.Permission], DateTime.UtcNow)
                        : Deny();
                });

            Repos.Evaluator.EvaluateBatchAsync(Arg.Any<IReadOnlyList<AuthorizationRequest>>(), Arg.Any<CancellationToken>())
                .Returns(call => call.Arg<IReadOnlyList<AuthorizationRequest>>()
                    .Select(r => allowedResourceIds.Contains(r.Context.ResourceId ?? Guid.Empty)
                        ? AuthorizationDecision.Allow("allow", [r.Permission], DateTime.UtcNow)
                        : Deny())
                    .ToList());
        }

        private static AuthorizationDecision Deny() =>
            AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.NoPermission, DateTime.UtcNow);
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

    private static Harness CreateHarness() => new();

    private static Document StubDocument(Guid? unitId, bool sensitive = false) =>
        Document.Create("Feast Agenda", "Draft", unitId, "person", ActorId, ActorId, DateTime.UtcNow);

    private static byte[] ContentBytes(string text = "content") =>
        System.Text.Encoding.UTF8.GetBytes(text);

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

    // --- 1. Denied operation fails and persists nothing ---

    [Fact]
    public async Task CreateDocument_is_denied_and_nothing_is_persisted_when_evaluator_denies()
    {
        var h = CreateHarness();
        h.DenyAll();

        var act = () => h.Sender.Send(new CreateDocumentCommand(
            ActorId, "Feast Agenda", "Draft", "person", ActorId, null, IsSensitive: false));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Documents.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    // --- 2. Unavailable evaluator fails closed ---

    [Fact]
    public async Task CreateDocument_is_denied_when_evaluator_is_unreachable()
    {
        var h = CreateHarness();
        h.Repos.Evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AuthorizationDecision>(new HttpRequestException("authz down")));

        var act = () => h.Sender.Send(new CreateDocumentCommand(
            ActorId, "Feast Agenda", "Draft", "person", ActorId, null, IsSensitive: false));

        await act.Should().ThrowAsync<Exception>();
        await h.Repos.Documents.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    // --- 3. Missing subject is denied (fail-closed) ---

    [Fact]
    public async Task Missing_subject_is_denied_by_the_guard()
    {
        var evaluator = Substitute.For<IAuthorizationEvaluator>();
        evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
                call.Arg<AuthorizationRequest>().SubjectId == Guid.Empty
                    ? AuthorizationDecision.Deny("d", AuthorizationDecisionReason.MissingSubject, DateTime.UtcNow)
                    : AuthorizationDecision.Allow("a", ["documents.document.create"], DateTime.UtcNow));

        var guard = new AuthorizationGuard(evaluator);

        var act = () => guard.RequireAsync(Guid.Empty, DocumentsPermissions.DocumentCreate,
            new AuthorizationContext(ResourceType: "document"), CancellationToken.None);

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
    }

    // --- 4..9. Token validation policy (see DocumentsJwtSecurityTests) ---

    // --- 10. Role claims never grant business authorization (see DocumentsJwtSecurityTests) ---

    // --- 11. Metadata and content are separate capabilities ---

    [Fact]
    public async Task Download_requires_content_read_and_never_the_metadata_read_permission()
    {
        var h = CreateHarness();
        var unit = Guid.NewGuid();
        var doc = StubDocument(unit);
        var now = DateTime.UtcNow;
        doc.AddVersion(Sha256(ContentBytes()), "text/plain", ContentBytes().Length, "a.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);
        h.Allow(DocumentsPermissions.DocumentRead, doc.Id, unit);

        var act = () => h.Sender.Send(new DownloadDocumentVersionQuery(ActorId, doc.Id, null));

        await act.Should().ThrowAsync<DocumentNotFoundException>();
        await h.Repos.Storage.DidNotReceiveWithAnyArgs().GetAsync(default!, default);
    }

    // --- 12. Sensitive content requires the extra sensitive permission ---

    [Fact]
    public async Task Download_of_sensitive_content_requires_content_read_sensitive()
    {
        var h = CreateHarness();
        var unit = Guid.NewGuid();
        var doc = StubDocument(unit);
        var now = DateTime.UtcNow;
        doc.AddVersion(Sha256(ContentBytes()), "text/plain", ContentBytes().Length, "a.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);
        doc.SetClassification("internal", isSensitive: true, null, null, null, ActorId, now);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);
        h.Allow(DocumentsPermissions.DocumentContentRead, doc.Id, unit);

        var act = () => h.Sender.Send(new DownloadDocumentVersionQuery(ActorId, doc.Id, null));

        await act.Should().ThrowAsync<DocumentNotFoundException>();
        await h.Repos.Storage.DidNotReceiveWithAnyArgs().GetAsync(default!, default);
    }

    [Fact]
    public async Task Download_of_sensitive_content_succeeds_with_the_extra_permission()
    {
        var h = CreateHarness();
        var unit = Guid.NewGuid();
        var doc = StubDocument(unit);
        var now = DateTime.UtcNow;
        var bytes = ContentBytes();
        doc.AddVersion(Sha256(bytes), "text/plain", bytes.Length, "a.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);
        doc.SetClassification("internal", isSensitive: true, null, null, null, ActorId, now);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);
        h.AllowAny(
        [
            DocumentsPermissions.DocumentContentRead,
            DocumentsPermissions.DocumentContentReadSensitive
        ], doc.Id, unit);
        h.Repos.Storage.GetAsync($"documents/{Sha256(bytes)}", Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(bytes));

        var result = await h.Sender.Send(new DownloadDocumentVersionQuery(ActorId, doc.Id, null));

        result.Should().NotBeNull();
        result.MimeType.Should().Be("text/plain");
        result.FileName.Should().Be("a.txt");
    }

    // --- 13. No enumeration oracle: denied reads are 404, not 403 ---

    [Fact]
    public async Task GetDocument_returns_not_found_when_read_is_denied()
    {
        var h = CreateHarness();
        var doc = StubDocument(Guid.NewGuid());
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);
        h.DenyAll();

        var act = () => h.Sender.Send(new GetDocumentQuery(ActorId, doc.Id));

        await act.Should().ThrowAsync<DocumentNotFoundException>();
    }

    [Fact]
    public async Task GetDocument_of_missing_document_also_returns_not_found()
    {
        var h = CreateHarness();
        h.DenyAll();

        var act = () => h.Sender.Send(new GetDocumentQuery(ActorId, Guid.NewGuid()));

        await act.Should().ThrowAsync<DocumentNotFoundException>();
    }

    // --- 14. Denied download leaks nothing ---

    [Fact]
    public async Task Download_leaks_no_content_when_content_read_is_denied()
    {
        var h = CreateHarness();
        var unit = Guid.NewGuid();
        var doc = StubDocument(unit);
        var now = DateTime.UtcNow;
        doc.AddVersion(Sha256(ContentBytes()), "text/plain", ContentBytes().Length, "a.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);
        h.DenyAll();

        var act = () => h.Sender.Send(new DownloadDocumentVersionQuery(ActorId, doc.Id, null));

        await act.Should().ThrowAsync<DocumentNotFoundException>();
        await h.Repos.Storage.DidNotReceiveWithAnyArgs().GetAsync(default!, default);
    }

    // --- 15. Unauthorized metadata update never persists ---

    [Fact]
    public async Task UpdateMetadata_is_denied_and_never_persists()
    {
        var h = CreateHarness();
        var unit = Guid.NewGuid();
        var doc = StubDocument(unit);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);
        h.DenyAll();

        var act = () => h.Sender.Send(new UpdateDocumentMetadataCommand(ActorId, doc.Id, "New", null));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Documents.DidNotReceive().UpdateAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>());
    }

    // --- 16. Unauthorized version create never touches storage ---

    [Fact]
    public async Task UploadVersion_is_denied_and_never_touches_object_storage()
    {
        var h = CreateHarness();
        var unit = Guid.NewGuid();
        var doc = StubDocument(unit);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);
        h.DenyAll();

        var act = () => h.Sender.Send(new UploadDocumentVersionCommand(
            ActorId, doc.Id, new MemoryStream(ContentBytes()), ContentBytes().Length, "a.txt",
            "text/plain", 50 * 1024 * 1024, ["text/plain"]));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Storage.DidNotReceiveWithAnyArgs().PutAsync(default!, default!, default!, default);
        await h.Repos.Storage.DidNotReceiveWithAnyArgs().ExistsAsync(default!, default);
        await h.Repos.Documents.DidNotReceive().UpdateAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>());
    }

    // --- 17. Held documents cannot be deactivated without an override ---

    [Fact]
    public async Task Deactivate_is_blocked_by_hold_without_an_admin_override()
    {
        var h = CreateHarness();
        var unit = Guid.NewGuid();
        var doc = StubDocument(unit);
        var now = DateTime.UtcNow;
        doc.AddVersion(Sha256(ContentBytes()), "text/plain", ContentBytes().Length, "a.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);
        doc.SetClassification(null, isSensitive: false, null, "legal-1", null, ActorId, now);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);
        h.Allow(DocumentsPermissions.DocumentDeactivate, doc.Id, unit);

        var act = () => h.Sender.Send(new DeactivateDocumentCommand(ActorId, doc.Id, AdminOverride: false, null));

        await act.Should().ThrowAsync<HeldDocumentDeactivationException>();
        await h.Repos.Documents.DidNotReceive().UpdateAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deactivate_with_override_requires_admin_permission()
    {
        var h = CreateHarness();
        var unit = Guid.NewGuid();
        var doc = StubDocument(unit);
        var now = DateTime.UtcNow;
        doc.AddVersion(Sha256(ContentBytes()), "text/plain", ContentBytes().Length, "a.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);
        doc.SetClassification(null, isSensitive: false, null, "legal-1", null, ActorId, now);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);
        h.Allow(DocumentsPermissions.DocumentDeactivate, doc.Id, unit);

        var act = () => h.Sender.Send(new DeactivateDocumentCommand(ActorId, doc.Id, AdminOverride: true, "release"));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Documents.DidNotReceive().UpdateAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>());
    }

    // --- 18. Restore requires permission ---

    [Fact]
    public async Task Restore_is_denied_without_restore_permission()
    {
        var h = CreateHarness();
        var unit = Guid.NewGuid();
        var doc = StubDocument(unit);
        var now = DateTime.UtcNow;
        doc.AddVersion(Sha256(ContentBytes()), "text/plain", ContentBytes().Length, "a.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);
        doc.Deactivate(ActorId, adminOverride: false, null, now);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);
        h.DenyAll();

        var act = () => h.Sender.Send(new RestoreDocumentCommand(ActorId, doc.Id));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Documents.DidNotReceive().UpdateAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>());
    }

    // --- 19. Organization scope is enforced ---

    [Fact]
    public async Task List_returns_only_documents_readable_at_their_scope()
    {
        var h = CreateHarness();
        var allowed = StubDocument(Guid.NewGuid());
        var denied = StubDocument(Guid.NewGuid());
        h.Repos.Documents.ListAsync(Arg.Any<CancellationToken>()).Returns([allowed, denied]);
        h.AllowBatchOnly(allowed.Id);

        var result = await h.Sender.Send(new ListDocumentsQuery(ActorId, null, null, null, null, null, null));

        result.Should().ContainSingle(d => d.Id == allowed.Id);
        result.Should().NotContain(d => d.Id == denied.Id);
    }

    [Fact]
    public async Task List_denies_when_the_coarse_read_permission_is_missing()
    {
        var h = CreateHarness();
        h.DenyAll();

        var act = () => h.Sender.Send(new ListDocumentsQuery(ActorId, null, null, null, null, null, null));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
    }

    [Fact]
    public async Task Download_uses_the_documents_primary_scope_in_the_authorization_context()
    {
        var h = CreateHarness();
        var unit = Guid.NewGuid();
        var doc = StubDocument(unit);
        var now = DateTime.UtcNow;
        doc.AddVersion(Sha256(ContentBytes()), "text/plain", ContentBytes().Length, "a.txt",
            DocumentSources.Member, ActorId, now, ScanStatus.NotScanned);
h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);
        h.Allow(DocumentsPermissions.DocumentContentRead, doc.Id, unit);
        h.Repos.Storage.GetAsync($"documents/{Sha256(ContentBytes())}", Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(ContentBytes()));

        var content = await h.Sender.Send(new DownloadDocumentVersionQuery(ActorId, doc.Id, null));

        // Allowed only when the context carries the document's primary unit.
        await h.Repos.Evaluator.Received(1).EvaluateAsync(Arg.Is<AuthorizationRequest>(r =>
                r.Permission == DocumentsPermissions.DocumentContentRead &&
                r.Context.OrganizationUnitId == unit &&
                r.Context.ResourceId == doc.Id),
            Arg.Any<CancellationToken>());
    }

    // --- 20. Binary content never appears in integration event payloads ---

    [Fact]
    public void Integration_events_never_carry_binary_content_or_object_keys()
    {
        var versionEvent = typeof(DocumentVersionAdded);
        var properties = versionEvent.GetProperties().Select(p => p.Name).ToList();

        properties.Should().Contain("ContentHash");
        properties.Should().NotContain("ObjectKey");
        properties.Should().NotContain("FileName");
        properties.Should().NotContain("Content");
        properties.Should().NotContain("Stream");

        foreach (var type in new[]
                 {
                     typeof(DocumentCreated),
                     typeof(DocumentVersionAdded),
                     typeof(DocumentClassified),
                     typeof(DocumentContentDownloaded)
                 })
        {
            type.GetProperties()
                .Should().NotContain(p => p.PropertyType == typeof(byte[]) ||
                                          p.PropertyType == typeof(Stream) ||
                                          p.PropertyType == typeof(MemoryStream));
        }
    }

    // --- Authorization is evaluated before any persistence or storage side effect ---

    [Fact]
    public async Task Classify_is_denied_before_persistence_when_permission_is_missing()
    {
        var h = CreateHarness();
        var unit = Guid.NewGuid();
        var doc = StubDocument(unit);
        h.Repos.Documents.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);
        h.Allow(DocumentsPermissions.DocumentMetadataUpdate, doc.Id, unit); // wrong permission

        var act = () => h.Sender.Send(new ClassifyDocumentCommand(
            ActorId, doc.Id, "internal", true, null, null, null));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Documents.DidNotReceive().UpdateAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_filters_fail_closed_even_when_some_rows_are_denied()
    {
        var h = CreateHarness();
        var allowed = StubDocument(Guid.NewGuid());
        h.Repos.Documents.ListAsync(Arg.Any<CancellationToken>()).Returns([allowed]);
        h.AllowBatchOnly(); // deny everything

        var result = await h.Sender.Send(new ListDocumentsQuery(ActorId, null, null, null, null, null, null));

        result.Should().BeEmpty();
    }
}
