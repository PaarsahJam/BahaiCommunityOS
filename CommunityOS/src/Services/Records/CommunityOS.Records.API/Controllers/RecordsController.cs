using Asp.Versioning;
using CommunityOS.Records.API.Extensions;
using CommunityOS.Records.Application.Commands;
using CommunityOS.Records.Application.DTOs;
using CommunityOS.Records.Application.Queries;
using CommunityOS.Records.Domain.Aggregates;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Records.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/records")]
[Authorize]
public sealed class RecordsController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RecordSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? category,
        [FromQuery] string? status,
        [FromQuery] Guid? organizationUnitId,
        [FromQuery] Guid? personId,
        [FromQuery] Guid? householdId,
        [FromQuery] bool? isSensitive,
        [FromQuery] string? query,
        CancellationToken ct)
    {
        var result = await sender.Send(new ListRecordsQuery(
            ActorId, category, status, organizationUnitId, personId, householdId, isSensitive, query), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RecordDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetRecordQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/sensitive")]
    [ProducesResponseType<IReadOnlyList<RecordFieldDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSensitiveFields(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetRecordSensitiveFieldsQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<RecordDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateRecordRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateRecordCommand(
            ActorId,
            request.Category,
            request.SubjectType,
            request.SubjectId,
            request.OrganizationUnitId,
            request.Fields.Select(f => RecordFieldValue.Create(f.FieldKey, f.FieldValue, f.IsSensitive)).ToList(),
            request.IsSensitive), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}/fields")]
    [ProducesResponseType<RecordDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateFields(Guid id, UpdateRecordFieldsRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateRecordFieldsCommand(
            ActorId, id,
            request.Fields.Select(f => RecordFieldValue.Create(f.FieldKey, f.FieldValue, f.IsSensitive)).ToList()), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType<RecordDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new SubmitRecordCommand(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/under-review")]
    [ProducesResponseType<RecordDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MoveUnderReview(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new MoveUnderReviewCommand(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/verify")]
    [ProducesResponseType<RecordDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Verify(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new VerifyRecordCommand(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<RecordDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new RejectRecordCommand(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/correct")]
    [ProducesResponseType<RecordDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Correct(Guid id, CorrectRecordRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CorrectRecordCommand(
            ActorId, id,
            request.Fields.Select(f => RecordFieldValue.Create(f.FieldKey, f.FieldValue, f.IsSensitive)).ToList(),
            request.ChangeReason), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/classify")]
    [ProducesResponseType<RecordDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Classify(Guid id, ClassifyRecordRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new ClassifyRecordCommand(
            ActorId, id, request.ClassificationCode, request.IsSensitive, request.RetentionScheduleCode), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType<RecordDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ArchiveRecordCommand(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<RecordDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(Guid id, DeactivateRecordRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new DeactivateRecordCommand(
            ActorId, id, request.AdminOverride, request.Reason), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType<RecordDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new RestoreRecordCommand(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/scopes")]
    [ProducesResponseType<RecordDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddScope(Guid id, AddScopeRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new AddRecordScopeCommand(ActorId, id, request.OrganizationUnitId), ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/scopes/{organizationUnitId:guid}")]
    [ProducesResponseType<RecordDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveScope(Guid id, Guid organizationUnitId, CancellationToken ct)
    {
        var result = await sender.Send(new RemoveRecordScopeCommand(ActorId, id, organizationUnitId), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/versions")]
    [ProducesResponseType<IReadOnlyList<RecordVersionDescriptorDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListVersions(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ListRecordVersionsQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/versions/{versionNumber:int}")]
    [ProducesResponseType<RecordVersionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersion(Guid id, int versionNumber, CancellationToken ct)
    {
        var result = await sender.Send(new GetRecordVersionQuery(ActorId, id, versionNumber), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/versions/{versionNumber:int}/sensitive")]
    [ProducesResponseType<IReadOnlyList<RecordFieldDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersionSensitiveFields(Guid id, int versionNumber, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetRecordVersionSensitiveFieldsQuery(ActorId, id, versionNumber), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/evidence")]
    [ProducesResponseType<IReadOnlyList<RecordEvidenceReferenceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListEvidence(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ListRecordEvidenceQuery(ActorId, id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/evidence")]
    [ProducesResponseType<RecordEvidenceReferenceDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AttachEvidence(Guid id, AttachEvidenceRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new AttachEvidenceCommand(
            ActorId, id, request.DocumentId, request.VersionNumber, request.ReferenceType), ct);
        return CreatedAtAction(nameof(ListEvidence), new { id }, result);
    }

    [HttpDelete("{id:guid}/evidence/{evidenceId:guid}")]
    [ProducesResponseType<RecordDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveEvidence(Guid id, Guid evidenceId, CancellationToken ct)
    {
        var result = await sender.Send(new RemoveEvidenceCommand(ActorId, id, evidenceId), ct);
        return Ok(result);
    }
}

public sealed record RecordFieldRequest(string FieldKey, string FieldValue, bool IsSensitive);

public sealed record CreateRecordRequest(
    string Category,
    string SubjectType,
    Guid SubjectId,
    Guid? OrganizationUnitId,
    IReadOnlyList<RecordFieldRequest> Fields,
    bool IsSensitive);

public sealed record UpdateRecordFieldsRequest(IReadOnlyList<RecordFieldRequest> Fields);

public sealed record CorrectRecordRequest(
    IReadOnlyList<RecordFieldRequest> Fields,
    string ChangeReason);

public sealed record ClassifyRecordRequest(
    string? ClassificationCode,
    bool IsSensitive,
    string? RetentionScheduleCode);

public sealed record DeactivateRecordRequest(bool AdminOverride, string? Reason);

public sealed record AddScopeRequest(Guid OrganizationUnitId);

public sealed record AttachEvidenceRequest(
    Guid DocumentId,
    int VersionNumber,
    string ReferenceType);