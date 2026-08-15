using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Documents.Application.Abstractions;
using CommunityOS.Documents.Application.DTOs;
using CommunityOS.Documents.Application.Logging;
using CommunityOS.Documents.Application.Permissions;
using CommunityOS.Documents.Application.Pipeline;
using CommunityOS.Documents.Domain.Aggregates;
using CommunityOS.Documents.Domain.Enumerations;
using CommunityOS.Documents.Domain.Exceptions;
using CommunityOS.Documents.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using static CommunityOS.Documents.Application.Commands.DocumentCommandHelpers;
using DocumentEventsPublisher = CommunityOS.Documents.Application.Pipeline.DomainEventPublisher;

namespace CommunityOS.Documents.Application.Commands;

public sealed record CreateDocumentCommand(
    Guid ActorId,
    string Title,
    string? Description,
    string? OwnerType,
    Guid? OwnerId,
    Guid? OrganizationUnitId,
    bool IsSensitive) : IRequest<DocumentDto>;

internal sealed class CreateDocumentCommandHandler(
    IDocumentRepository documents,
    IOrganizationUnitReferenceRepository units,
    AuthorizationGuard guard,
    IMediator mediator,
    ILogger<CreateDocumentCommandHandler> logger)
    : IRequestHandler<CreateDocumentCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(CreateDocumentCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, DocumentsPermissions.DocumentCreate,
            new AuthorizationContext(ResourceType: "document"), ct);

        await EnsureUnitExistsAsync(units, cmd.OrganizationUnitId, ct);
        if (string.Equals(cmd.OwnerType, DocumentOwnerTypes.OrganizationUnit, StringComparison.OrdinalIgnoreCase))
            await EnsureUnitExistsAsync(units, cmd.OwnerId, ct);

        var document = Document.Create(
            cmd.Title,
            cmd.Description,
            cmd.OrganizationUnitId,
            cmd.OwnerType,
            cmd.OwnerId,
            cmd.ActorId,
            DateTime.UtcNow);

        if (cmd.IsSensitive)
        {
            document.SetClassification(
                classificationCode: null,
                isSensitive: true,
                retentionCategory: null,
                legalHoldReference: null,
                administrativeHoldReference: null,
                classifiedBy: cmd.ActorId,
                occurredOn: DateTime.UtcNow);
        }

        await documents.AddAsync(document, ct);
        logger.DocumentCreated(document.Id, document.Title);

        await DocumentEventsPublisher.PublishAsync(document, mediator, ct);
        return document.ToDto();
    }

    internal static async Task EnsureUnitExistsAsync(
        IOrganizationUnitReferenceRepository units, Guid? unitId, CancellationToken ct)
    {
        if (unitId is null)
            return;

        var reference = await units.GetUnitByIdAsync(unitId.Value, ct);
        if (reference is null)
            throw new InvalidDocumentScopeException(unitId.Value);
    }
}

public sealed record UpdateDocumentMetadataCommand(
    Guid ActorId,
    Guid DocumentId,
    string Title,
    string? Description) : IRequest<DocumentDto>;

internal sealed class UpdateDocumentMetadataCommandHandler(
    IDocumentRepository documents,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<UpdateDocumentMetadataCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(UpdateDocumentMetadataCommand cmd, CancellationToken ct)
    {
        var document = await LoadForMutationAsync(documents, cmd.DocumentId, ct);
        await guard.RequireAsync(cmd.ActorId, DocumentsPermissions.DocumentMetadataUpdate,
            ContextFor(document), ct);

        document.UpdateMetadata(cmd.Title, cmd.Description, cmd.ActorId, DateTime.UtcNow);
        await documents.UpdateAsync(document, ct);
        await DocumentEventsPublisher.PublishAsync(document, mediator, ct);

        return document.ToDto();
    }
}

public sealed record ClassifyDocumentCommand(
    Guid ActorId,
    Guid DocumentId,
    string? ClassificationCode,
    bool IsSensitive,
    string? RetentionCategory,
    string? LegalHoldReference,
    string? AdministrativeHoldReference) : IRequest<DocumentDto>;

internal sealed class ClassifyDocumentCommandHandler(
    IDocumentRepository documents,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<ClassifyDocumentCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(ClassifyDocumentCommand cmd, CancellationToken ct)
    {
        var document = await LoadForMutationAsync(documents, cmd.DocumentId, ct);
        await guard.RequireAsync(cmd.ActorId, DocumentsPermissions.DocumentClassify,
            ContextFor(document), ct);

        document.SetClassification(
            cmd.ClassificationCode,
            cmd.IsSensitive,
            cmd.RetentionCategory,
            cmd.LegalHoldReference,
            cmd.AdministrativeHoldReference,
            cmd.ActorId,
            DateTime.UtcNow);
        await documents.UpdateAsync(document, ct);
        await DocumentEventsPublisher.PublishAsync(document, mediator, ct);

        return document.ToDto();
    }
}

public sealed record AddDocumentScopeCommand(
    Guid ActorId,
    Guid DocumentId,
    Guid OrganizationUnitId) : IRequest<DocumentDto>;

internal sealed class AddDocumentScopeCommandHandler(
    IDocumentRepository documents,
    IOrganizationUnitReferenceRepository units,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<AddDocumentScopeCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(AddDocumentScopeCommand cmd, CancellationToken ct)
    {
        var document = await LoadForMutationAsync(documents, cmd.DocumentId, ct);
        await guard.RequireAsync(cmd.ActorId, DocumentsPermissions.DocumentScopeManage,
            ContextFor(document), ct);

        await CreateDocumentCommandHandler.EnsureUnitExistsAsync(units, cmd.OrganizationUnitId, ct);

        document.AddOrganizationScope(cmd.OrganizationUnitId, cmd.ActorId, DateTime.UtcNow);
        await documents.UpdateAsync(document, ct);
        await DocumentEventsPublisher.PublishAsync(document, mediator, ct);

        return document.ToDto();
    }
}

public sealed record RemoveDocumentScopeCommand(
    Guid ActorId,
    Guid DocumentId,
    Guid OrganizationUnitId) : IRequest<DocumentDto>;

internal sealed class RemoveDocumentScopeCommandHandler(
    IDocumentRepository documents,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<RemoveDocumentScopeCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(RemoveDocumentScopeCommand cmd, CancellationToken ct)
    {
        var document = await LoadForMutationAsync(documents, cmd.DocumentId, ct);
        await guard.RequireAsync(cmd.ActorId, DocumentsPermissions.DocumentScopeManage,
            ContextFor(document), ct);

        document.RemoveOrganizationScope(cmd.OrganizationUnitId, cmd.ActorId, DateTime.UtcNow);
        await documents.UpdateAsync(document, ct);
        await DocumentEventsPublisher.PublishAsync(document, mediator, ct);

        return document.ToDto();
    }
}

public sealed record UploadDocumentVersionCommand(
    Guid ActorId,
    Guid DocumentId,
    Stream Content,
    long Length,
    string FileName,
    string ContentType,
    long MaxFileSizeBytes,
    IReadOnlyList<string> AllowedMimeTypes) : IRequest<DocumentVersionDto>;

internal sealed class UploadDocumentVersionCommandHandler(
    IDocumentRepository documents,
    IDocumentObjectStorage storage,
    IDocumentScanService scanner,
    AuthorizationGuard guard,
    IMediator mediator,
    ILogger<UploadDocumentVersionCommandHandler> logger)
    : IRequestHandler<UploadDocumentVersionCommand, DocumentVersionDto>
{
    public async Task<DocumentVersionDto> Handle(UploadDocumentVersionCommand cmd, CancellationToken ct)
    {
        var document = await LoadForMutationAsync(documents, cmd.DocumentId, ct);
        await guard.RequireAsync(cmd.ActorId, DocumentsPermissions.DocumentVersionCreate,
            ContextFor(document), ct);

        var content = await RewindAsync(cmd.Content, ct);

        var (contentHash, sizeBytes) = await ComputeSha256Async(content, ct);

        var contentType = (cmd.ContentType ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(contentType) ||
            !cmd.AllowedMimeTypes.Any(m => string.Equals(m, contentType, StringComparison.OrdinalIgnoreCase)))
            throw new UnsupportedDocumentMimeTypeException(cmd.ContentType ?? string.Empty);

        if (sizeBytes > cmd.MaxFileSizeBytes)
            throw new DocumentContentTooLargeException(cmd.MaxFileSizeBytes);

        var existing = document.Versions.FirstOrDefault(v => v.ContentHash == contentHash);
        if (existing is not null)
            return existing.ToDto();

        var objectKey = $"documents/{contentHash}";

        var scanStatus = await scanner.ScanAsync(content, ct);
        content.Position = 0;

        var stored = await storage.ExistsAsync(objectKey, ct);
        if (!stored)
        {
            content.Position = 0;
            await storage.PutAsync(objectKey, content, contentType, ct);
        }

        DocumentVersion version;
        try
        {
            version = document.AddVersion(
                contentHash,
                contentType,
                sizeBytes,
                cmd.FileName,
                DocumentSources.Member,
                cmd.ActorId,
                DateTime.UtcNow,
                scanStatus);

            await documents.UpdateAsync(document, ct);
        }
        catch
        {
            if (!stored)
                await storage.DeleteAsync(objectKey, CancellationToken.None);
            throw;
        }

        logger.DocumentVersionAdded(document.Id, version.VersionNumber, version.ContentHash);
        await DocumentEventsPublisher.PublishAsync(document, mediator, ct);

        return version.ToDto();
    }

    private static async Task<Stream> RewindAsync(Stream content, CancellationToken ct)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
            return content;
        }

        var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        return buffer;
    }

    private static async Task<(string Hash, long Size)> ComputeSha256Async(Stream content, CancellationToken ct)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var hashing = new System.Security.Cryptography.CryptoStream(
            Stream.Null, sha, System.Security.Cryptography.CryptoStreamMode.Write);
        await content.CopyToAsync(hashing, ct);
        hashing.FlushFinalBlock();

        return (Convert.ToHexString(sha.Hash!).ToLowerInvariant(), content.Length);
    }
}

public sealed record ArchiveDocumentCommand(Guid ActorId, Guid DocumentId) : IRequest<DocumentDto>;

internal sealed class ArchiveDocumentCommandHandler(
    IDocumentRepository documents,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<ArchiveDocumentCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(ArchiveDocumentCommand cmd, CancellationToken ct)
    {
        var document = await LoadForMutationAsync(documents, cmd.DocumentId, ct);
        await guard.RequireAsync(cmd.ActorId, DocumentsPermissions.DocumentArchive,
            ContextFor(document), ct);

        document.Archive(cmd.ActorId, DateTime.UtcNow);
        await documents.UpdateAsync(document, ct);
        await DocumentEventsPublisher.PublishAsync(document, mediator, ct);

        return document.ToDto();
    }
}

public sealed record DeactivateDocumentCommand(
    Guid ActorId,
    Guid DocumentId,
    bool AdminOverride,
    string? Reason) : IRequest<DocumentDto>;

internal sealed class DeactivateDocumentCommandHandler(
    IDocumentRepository documents,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<DeactivateDocumentCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(DeactivateDocumentCommand cmd, CancellationToken ct)
    {
        var document = await LoadForMutationAsync(documents, cmd.DocumentId, ct);
        var context = ContextFor(document);

        await guard.RequireAsync(cmd.ActorId, DocumentsPermissions.DocumentDeactivate, context, ct);
        if (cmd.AdminOverride)
            await guard.RequireAsync(cmd.ActorId, DocumentsPermissions.DocumentAdmin, context, ct);

        document.Deactivate(cmd.ActorId, cmd.AdminOverride, cmd.Reason, DateTime.UtcNow);
        await documents.UpdateAsync(document, ct);
        await DocumentEventsPublisher.PublishAsync(document, mediator, ct);

        return document.ToDto();
    }
}

public sealed record RestoreDocumentCommand(Guid ActorId, Guid DocumentId) : IRequest<DocumentDto>;

internal sealed class RestoreDocumentCommandHandler(
    IDocumentRepository documents,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<RestoreDocumentCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(RestoreDocumentCommand cmd, CancellationToken ct)
    {
        var document = await LoadForMutationAsync(documents, cmd.DocumentId, ct);
        await guard.RequireAsync(cmd.ActorId, DocumentsPermissions.DocumentRestore,
            ContextFor(document), ct);

        document.Restore(cmd.ActorId, DateTime.UtcNow);
        await documents.UpdateAsync(document, ct);
        await DocumentEventsPublisher.PublishAsync(document, mediator, ct);

        return document.ToDto();
    }
}

public sealed record CreateDocumentReferenceCommand(
    Guid ActorId,
    Guid DocumentId,
    string SourceContext,
    Guid SourceEntityId,
    string ReferenceType) : IRequest<DocumentReferenceDto>;

internal sealed class CreateDocumentReferenceCommandHandler(
    IDocumentRepository documents,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<CreateDocumentReferenceCommand, DocumentReferenceDto>
{
    public async Task<DocumentReferenceDto> Handle(CreateDocumentReferenceCommand cmd, CancellationToken ct)
    {
        var document = await LoadForMutationAsync(documents, cmd.DocumentId, ct);
        await guard.RequireAsync(cmd.ActorId, DocumentsPermissions.DocumentReference,
            ContextFor(document), ct);

        var reference = document.AddReference(
            cmd.SourceContext, cmd.SourceEntityId, cmd.ReferenceType, cmd.ActorId, DateTime.UtcNow);

        await documents.UpdateAsync(document, ct);
        await DocumentEventsPublisher.PublishAsync(document, mediator, ct);

        return reference.ToDto();
    }
}

internal static class DocumentCommandHelpers
{
    /// <summary>
    /// Loads the document for a guarded mutation. Ordering guarantees that
    /// authorization is evaluated before any persistence or storage side
    /// effect: the resource is loaded, then guarded, then mutated and saved.
    /// </summary>
    public static async Task<Document> LoadForMutationAsync(
        IDocumentRepository documents, Guid documentId, CancellationToken ct) =>
        await documents.GetByIdAsync(documentId, ct)
        ?? throw new DocumentNotFoundException(documentId);

    /// <summary>
    /// Builds the resource-level authorization context: <c>resourceType =
    /// "document"</c>, the document id, and the primary organization scope
    /// (default; the Authorization service composes any-scope access).
    /// </summary>
    public static AuthorizationContext ContextFor(Document document) =>
        new(document.OrganizationUnitId, "document", document.Id);
}


