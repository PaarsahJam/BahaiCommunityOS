using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Documents.Application.Abstractions;
using CommunityOS.Documents.Application.DTOs;
using CommunityOS.Documents.Application.Options;
using CommunityOS.Documents.Application.Permissions;
using CommunityOS.Documents.Application.Pipeline;
using CommunityOS.Documents.Domain.Aggregates;
using CommunityOS.Documents.Domain.Exceptions;
using CommunityOS.Documents.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Options;

namespace CommunityOS.Documents.Application.Queries;

public sealed record ListDocumentsQuery(
    Guid ActorId,
    Guid? OrganizationUnitId,
    string? OwnerType,
    Guid? OwnerId,
    string? Status,
    bool? IsSensitive,
    string? Query) : IRequest<IReadOnlyList<DocumentSummaryDto>>;

internal sealed class ListDocumentsQueryHandler(
    IDocumentRepository documents,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator)
    : IRequestHandler<ListDocumentsQuery, IReadOnlyList<DocumentSummaryDto>>
{
    public async Task<IReadOnlyList<DocumentSummaryDto>> Handle(ListDocumentsQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, DocumentsPermissions.DocumentRead,
            query.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "document")
                : new AuthorizationContext(ResourceType: "document"), ct);

        var candidates = query.OrganizationUnitId is { } filterUnit
            ? await documents.ListByOrganizationUnitAsync(filterUnit, ct)
            : await documents.ListAsync(ct);

        var filtered = candidates
            .Where(d => query.Status is null || string.Equals(d.Status.Name, query.Status, StringComparison.OrdinalIgnoreCase))
            .Where(d => query.IsSensitive is null || d.Classification.IsSensitive == query.IsSensitive)
            .Where(d => query.OwnerType is null || string.Equals(d.OwnerType, query.OwnerType, StringComparison.OrdinalIgnoreCase))
            .Where(d => query.OwnerId is null || d.OwnerId == query.OwnerId)
            .Where(d => query.Query is null
                || d.Title.Contains(query.Query, StringComparison.OrdinalIgnoreCase)
                || (d.Description?.Contains(query.Query, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();

        // Fail-closed read filtering at the query boundary: only documents the
        // caller may read are returned; nothing reveals the existence or count
        // of unauthorized documents (ADR-022).
        var requests = filtered
            .Select(d => new AuthorizationRequest(
                query.ActorId,
                DocumentsPermissions.DocumentRead,
                new AuthorizationContext(d.OrganizationUnitId, "document", d.Id)))
            .ToList();

        var decisions = await evaluator.EvaluateBatchAsync(requests, ct);

        return filtered
            .Where((d, i) => decisions[i].Allowed)
            .OrderByDescending(d => d.UpdatedOn)
            .Select(d => d.ToSummaryDto())
            .ToList()
            .AsReadOnly();
    }
}

public sealed record GetDocumentQuery(Guid ActorId, Guid DocumentId) : IRequest<DocumentDto>;

internal sealed class GetDocumentQueryHandler(
    IDocumentRepository documents,
    AuthorizationGuard guard)
    : IRequestHandler<GetDocumentQuery, DocumentDto>
{
    public async Task<DocumentDto> Handle(GetDocumentQuery query, CancellationToken ct)
    {
        var document = await LoadOrNothingAsync(documents, query.DocumentId, ct);

        if (!await guard.HasAsync(query.ActorId, DocumentsPermissions.DocumentRead,
                new AuthorizationContext(document.OrganizationUnitId, "document", document.Id), ct))
            throw new DocumentNotFoundException(query.DocumentId);

        return document.ToDto();
    }

    internal static async Task<Document> LoadOrNothingAsync(
        IDocumentRepository documents, Guid documentId, CancellationToken ct) =>
        await documents.GetByIdAsync(documentId, ct)
        ?? throw new DocumentNotFoundException(documentId);
}

public sealed record ListDocumentVersionsQuery(Guid ActorId, Guid DocumentId)
    : IRequest<IReadOnlyList<DocumentVersionDto>>;

internal sealed class ListDocumentVersionsQueryHandler(
    IDocumentRepository documents,
    AuthorizationGuard guard)
    : IRequestHandler<ListDocumentVersionsQuery, IReadOnlyList<DocumentVersionDto>>
{
    public async Task<IReadOnlyList<DocumentVersionDto>> Handle(
        ListDocumentVersionsQuery query, CancellationToken ct)
    {
        var document = await GetDocumentQueryHandler.LoadOrNothingAsync(documents, query.DocumentId, ct);

        if (!await guard.HasAsync(query.ActorId, DocumentsPermissions.DocumentRead,
                new AuthorizationContext(document.OrganizationUnitId, "document", document.Id), ct))
            throw new DocumentNotFoundException(query.DocumentId);

        return document.Versions
            .OrderBy(v => v.VersionNumber)
            .Select(v => v.ToDto())
            .ToList()
            .AsReadOnly();
    }
}

public sealed record GetDocumentVersionQuery(Guid ActorId, Guid DocumentId, int VersionNumber)
    : IRequest<DocumentVersionDto>;

internal sealed class GetDocumentVersionQueryHandler(
    IDocumentRepository documents,
    AuthorizationGuard guard)
    : IRequestHandler<GetDocumentVersionQuery, DocumentVersionDto>
{
    public async Task<DocumentVersionDto> Handle(GetDocumentVersionQuery query, CancellationToken ct)
    {
        var document = await GetDocumentQueryHandler.LoadOrNothingAsync(documents, query.DocumentId, ct);

        if (!await guard.HasAsync(query.ActorId, DocumentsPermissions.DocumentRead,
                new AuthorizationContext(document.OrganizationUnitId, "document", document.Id), ct))
            throw new DocumentNotFoundException(query.DocumentId);

        var version = document.Versions.FirstOrDefault(v => v.VersionNumber == query.VersionNumber)
            ?? throw new DocumentVersionNotFoundException(query.DocumentId, query.VersionNumber);

        return version.ToDto();
    }
}

public sealed record ListDocumentReferencesQuery(Guid ActorId, Guid DocumentId)
    : IRequest<IReadOnlyList<DocumentReferenceDto>>;

internal sealed class ListDocumentReferencesQueryHandler(
    IDocumentRepository documents,
    AuthorizationGuard guard)
    : IRequestHandler<ListDocumentReferencesQuery, IReadOnlyList<DocumentReferenceDto>>
{
    public async Task<IReadOnlyList<DocumentReferenceDto>> Handle(
        ListDocumentReferencesQuery query, CancellationToken ct)
    {
        var document = await GetDocumentQueryHandler.LoadOrNothingAsync(documents, query.DocumentId, ct);

        if (!await guard.HasAsync(query.ActorId, DocumentsPermissions.DocumentReferenceRead,
                new AuthorizationContext(document.OrganizationUnitId, "document", document.Id), ct))
            throw new DocumentNotFoundException(query.DocumentId);

        return document.References
            .Select(r => r.ToDto())
            .ToList()
            .AsReadOnly();
    }
}

public sealed record DownloadDocumentVersionQuery(
    Guid ActorId,
    Guid DocumentId,
    int? VersionNumber) : IRequest<DocumentContentDto>;

internal sealed class DownloadDocumentVersionQueryHandler(
    IDocumentRepository documents,
    IDocumentObjectStorage storage,
    AuthorizationGuard guard,
    IOptions<DocumentsIntegrityOptions> integrityOptions,
    IMediator mediator)
    : IRequestHandler<DownloadDocumentVersionQuery, DocumentContentDto>
{
    private readonly DocumentsIntegrityOptions _integrity = integrityOptions.Value;
    public async Task<DocumentContentDto> Handle(DownloadDocumentVersionQuery query, CancellationToken ct)
    {
        var document = await GetDocumentQueryHandler.LoadOrNothingAsync(documents, query.DocumentId, ct);

        // Indistinguishable not-found behavior: a caller without read access
        // (or without sensitive access) receives the same 404 as a missing
        // document — no existence oracle (ADR-022).
        if (!await guard.HasAsync(query.ActorId, DocumentsPermissions.DocumentContentRead,
                new AuthorizationContext(document.OrganizationUnitId, "document", document.Id), ct))
            throw new DocumentNotFoundException(query.DocumentId);

        if (document.Classification.IsSensitive &&
            !await guard.HasAsync(query.ActorId, DocumentsPermissions.DocumentContentReadSensitive,
                new AuthorizationContext(document.OrganizationUnitId, "document", document.Id), ct))
            throw new DocumentNotFoundException(query.DocumentId);

        var version = query.VersionNumber is { } requested
            ? document.Versions.FirstOrDefault(v => v.VersionNumber == requested)
                ?? throw new DocumentVersionNotFoundException(document.Id, requested)
            : document.CurrentVersion
                ?? throw new DocumentNotFoundException(document.Id);

        if (version.ScanStatus.BlocksDownload)
            throw new ContentNotDownloadableException(document.Id, version.Id, version.ScanStatus.Name);

        var stream = await storage.GetAsync(version.ObjectKey, ct)
            ?? throw new DocumentVersionNotFoundException(document.Id, version.VersionNumber);

        if (_integrity.VerifyHashOnRead)
            stream = await VerifyHashAsync(stream, document.Id, version, ct);

        if (document.Classification.IsSensitive)
            await mediator.Publish(
                new DocumentContentDownloadNotification(document.Id, version.Id, query.ActorId), ct);

        return new DocumentContentDto(stream, version.MimeType, version.FileName, version.SizeBytes, version.Id);
    }

    /// <summary>
    /// Recomputes the SHA-256 of the stored content and compares it with the
    /// version's recorded hash before serving (ADR-022, ratified). A mismatch
    /// means object-storage corruption or tampering; the content is never
    /// served. Buffered in memory because content is capped at the maximum
    /// upload size and a mismatch must be detected before any byte is streamed.
    /// </summary>
    private static async Task<Stream> VerifyHashAsync(
        Stream content, Guid documentId, DocumentVersion version, CancellationToken ct)
    {
        var buffer = new MemoryStream();
        try
        {
            await content.CopyToAsync(buffer, ct);
            buffer.Position = 0;

            using var sha = System.Security.Cryptography.SHA256.Create();
            var actualHash = Convert.ToHexString(sha.ComputeHash(buffer)).ToLowerInvariant();

            if (!string.Equals(actualHash, version.ContentHash, StringComparison.OrdinalIgnoreCase))
                throw new DocumentIntegrityViolationException(documentId, version.Id);

            buffer.Position = 0;
            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }
}