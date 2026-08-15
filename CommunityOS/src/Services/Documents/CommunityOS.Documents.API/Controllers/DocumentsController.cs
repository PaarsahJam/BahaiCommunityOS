using Asp.Versioning;
using CommunityOS.Documents.API.Config;
using CommunityOS.Documents.API.Extensions;
using CommunityOS.Documents.Application.Commands;
using CommunityOS.Documents.Application.DTOs;
using CommunityOS.Documents.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CommunityOS.Documents.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/documents")]
[Authorize]
public sealed class DocumentsController(ISender sender, IOptions<DocumentsApiOptions> options) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DocumentSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? organizationUnitId,
        [FromQuery] string? ownerType,
        [FromQuery] Guid? ownerId,
        [FromQuery] string? status,
        [FromQuery] bool? isSensitive,
        [FromQuery] string? query,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new ListDocumentsQuery(ActorId, organizationUnitId, ownerType, ownerId, status, isSensitive, query), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetDocumentQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateDocumentRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateDocumentCommand(
            ActorId,
            request.Title,
            request.Description,
            request.Owner?.OwnerType,
            request.Owner?.OwnerId,
            request.OrganizationUnitId,
            request.IsSensitive), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}/metadata")]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMetadata(Guid id, UpdateDocumentMetadataRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateDocumentMetadataCommand(ActorId, id, request.Title, request.Description), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/classify")]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Classify(Guid id, ClassifyDocumentRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new ClassifyDocumentCommand(
            ActorId, id, request.ClassificationCode, request.IsSensitive, request.RetentionCategory,
            request.LegalHoldReference, request.AdministrativeHoldReference), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/scopes")]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddScope(Guid id, AddScopeRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new AddDocumentScopeCommand(ActorId, id, request.OrganizationUnitId), ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/scopes/{organizationUnitId:guid}")]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveScope(Guid id, Guid organizationUnitId, CancellationToken ct)
    {
        var result = await sender.Send(new RemoveDocumentScopeCommand(ActorId, id, organizationUnitId), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ArchiveDocumentCommand(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(Guid id, DeactivateDocumentRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new DeactivateDocumentCommand(ActorId, id, request.AdminOverride, request.Reason), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new RestoreDocumentCommand(ActorId, id), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/versions")]
    [ProducesResponseType<IReadOnlyList<DocumentVersionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListVersions(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ListDocumentVersionsQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/versions/{versionNumber:int}")]
    [ProducesResponseType<DocumentVersionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersion(Guid id, int versionNumber, CancellationToken ct)
    {
        var result = await sender.Send(new GetDocumentVersionQuery(ActorId, id, versionNumber), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/versions")]
    [ProducesResponseType<DocumentVersionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    [RequestSizeLimit(80 * 1024 * 1024)]
    public async Task<IActionResult> UploadVersion(Guid id, IFormFile file, CancellationToken ct)
    {
        var result = await sender.Send(new UploadDocumentVersionCommand(
            ActorId,
            id,
            file.OpenReadStream(),
            file.Length,
            file.FileName,
            file.ContentType,
            options.Value.MaxFileSizeBytes,
            options.Value.AllowedMimeTypes), ct);

        return CreatedAtAction(nameof(GetVersion), new { id, versionNumber = result.VersionNumber }, result);
    }

    [HttpGet("{id:guid}/content")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadCurrent(Guid id, CancellationToken ct) =>
        await DownloadAsync(id, null, ct);

    [HttpGet("{id:guid}/versions/{versionNumber:int}/content")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadVersion(Guid id, int versionNumber, CancellationToken ct) =>
        await DownloadAsync(id, versionNumber, ct);

    private async Task<IActionResult> DownloadAsync(Guid id, int? versionNumber, CancellationToken ct)
    {
        var content = await sender.Send(
            new DownloadDocumentVersionQuery(ActorId, id, versionNumber), ct);

        return new FileStreamResult(content.Content, content.MimeType)
        {
            FileDownloadName = content.FileName,
            EnableRangeProcessing = true
        };
    }

    [HttpGet("{id:guid}/references")]
    [ProducesResponseType<IReadOnlyList<DocumentReferenceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListReferences(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ListDocumentReferencesQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/references")]
    [ProducesResponseType<DocumentReferenceDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateReference(Guid id, CreateReferenceRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateDocumentReferenceCommand(
            ActorId, id, request.SourceContext, request.SourceEntityId, request.ReferenceType), ct);
        return CreatedAtAction(nameof(ListReferences), new { id }, result);
    }
}

public sealed record OwnerRequest(string OwnerType, Guid OwnerId);

public sealed record CreateDocumentRequest(
    string Title,
    string? Description,
    OwnerRequest? Owner,
    Guid? OrganizationUnitId,
    bool IsSensitive);

public sealed record UpdateDocumentMetadataRequest(string Title, string? Description);

public sealed record ClassifyDocumentRequest(
    string? ClassificationCode,
    bool IsSensitive,
    string? RetentionCategory,
    string? LegalHoldReference,
    string? AdministrativeHoldReference);

public sealed record AddScopeRequest(Guid OrganizationUnitId);

public sealed record DeactivateDocumentRequest(bool AdminOverride, string? Reason);

public sealed record CreateReferenceRequest(
    string SourceContext,
    Guid SourceEntityId,
    string ReferenceType);