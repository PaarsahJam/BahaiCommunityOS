using Asp.Versioning;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityOS.Audit.API.Extensions;
using CommunityOS.Audit.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Audit.API.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/audit")]
public sealed class AuditController(IMediator mediator) : ControllerBase
{
    private const string CsvContentType = "text/csv; charset=utf-8";
    private const string NdjsonContentType = "application/x-ndjson; charset=utf-8";

    // ---- Query -------------------------------------------------------------

    [HttpGet]
    public async Task<ActionResult<AuditPageDto>> Query(
        [FromQuery] string? sourceService,
        [FromQuery] string? eventType,
        [FromQuery] string? action,
        [FromQuery] string? resourceType,
        [FromQuery] Guid? resourceId,
        [FromQuery] Guid? subjectId,
        [FromQuery] Guid? actorId,
        [FromQuery] Guid? organizationUnitId,
        [FromQuery] DateTime? occurredFrom,
        [FromQuery] DateTime? occurredTo,
        [FromQuery] string order = "desc",
        [FromQuery] int? limit = null,
        [FromQuery] int offset = 0,
        [FromQuery] bool includeSensitive = false,
        CancellationToken ct = default)
    {
        var query = new AuditQuery(
            User.SubjectId(),
            new AuditQueryFilters(sourceService, eventType, action, resourceType, resourceId,
                subjectId, actorId, organizationUnitId, occurredFrom, occurredTo),
            order, includeSensitive, limit, offset);
        return Ok(await mediator.Send(query, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AuditEntryDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetAuditEntry(User.SubjectId(), id), ct));

    // ---- Export ------------------------------------------------------------

    public sealed record ExportRequestBody(
        string Format,
        AuditQueryFiltersDto Filters,
        bool IncludeSensitive = false,
        int? MaxRows = null);

    public sealed record AuditQueryFiltersDto(
        string? SourceService, string? EventType, string? Action, string? ResourceType,
        Guid? ResourceId, Guid? SubjectId, Guid? ActorId, Guid? OrganizationUnitId,
        DateTime? OccurredFrom, DateTime? OccurredTo)
    {
        public AuditQueryFilters ToFilters() =>
            new(SourceService, EventType, Action, ResourceType, ResourceId,
                SubjectId, ActorId, OrganizationUnitId, OccurredFrom, OccurredTo);
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export(
        [FromBody] ExportRequestBody body,
        CancellationToken ct)
    {
        if (body is null || body.Filters is null)
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        var result = await mediator.Send(new ExportAuditCommand(
            User.SubjectId(),
            body.Format ?? "csv",
            body.Filters.ToFilters(),
            body.IncludeSensitive,
            body.MaxRows), ct);

        var (contentType, fileName) = result.Format == "ndjson"
            ? (NdjsonContentType, $"audit-export-{DateTime.UtcNow:yyyyMMddTHHmmssZ}.ndjson")
            : (CsvContentType, $"audit-export-{DateTime.UtcNow:yyyyMMddTHHmmssZ}.csv");

        Response.ContentType = contentType;
        Response.ContentLength = null;
        Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";

        await using var writer = new StreamWriter(Response.Body, new UTF8Encoding(false), leaveOpen: true);
        if (result.Format == "ndjson")
        {
            foreach (var row in result.Rows)
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(ExportDto(row), AuditJsonOptions.Export));
            }
        }
        else
        {
            await WriteCsvAsync(writer, result.Rows);
        }

        return Empty;
    }

    // ---- Holds ---------------------------------------------------------------

    public sealed record PlaceHoldBody(IReadOnlyList<Guid> EntryIds, string HoldType, string ReasonCode);

    [HttpPost("holds")]
    public async Task<IActionResult> PlaceHold([FromBody] PlaceHoldBody body, CancellationToken ct)
    {
        if (body is null)
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        var holds = await mediator.Send(
            new PlaceHoldCommand(User.SubjectId(), body.EntryIds, body.HoldType, body.ReasonCode), ct);
        return Created(string.Empty, holds);
    }

    [HttpPost("holds/{id:guid}/release")]
    public async Task<IActionResult> ReleaseHold(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new ReleaseHoldCommand(User.SubjectId(), id), ct));

    // ---- Retention purge -------------------------------------------------------

    public sealed record PurgeBody(int MaxBatchSize = 0);

    [HttpPost("admin/purge-expired")]
    public async Task<IActionResult> PurgeExpired([FromBody] PurgeBody? body, CancellationToken ct) =>
        Ok(await mediator.Send(new PurgeExpiredCommand(User.SubjectId(), body?.MaxBatchSize ?? 0), ct));

    // ---- Formatting helpers ------------------------------------------------------

    internal static object ExportDto(AuditEntryRow row) => new
    {
        id = row.Id,
        occurredOn = row.OccurredOn,
        ingestedOn = row.IngestedOn,
        sourceService = row.SourceService,
        sourceEventType = row.SourceEventType,
        action = row.Action,
        outcome = row.Outcome,
        resourceType = row.ResourceType,
        resourceId = row.ResourceId,
        secondaryResourceId = row.SecondaryResourceId,
        subjectId = row.SubjectId,
        actorId = row.ActorId,
        organizationUnitId = row.OrganizationUnitId,
        sensitivity = row.Sensitivity == Domain.AuditSensitivity.Sensitive ? "sensitive" : "normal",
        metadata = ParseMetadata(row.MetadataJson),
        retentionClass = row.RetentionClass,
        retentionExpiresOn = row.RetentionExpiresOn
    };

    private static Dictionary<string, object?> ParseMetadata(string? json) =>
        string.IsNullOrEmpty(json)
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? [];

    private static async Task WriteCsvAsync(TextWriter writer, IReadOnlyList<AuditEntryRow> rows)
    {
        await writer.WriteLineAsync(
            "id,occurred_on,ingested_on,source_service,source_event_type,action,outcome," +
            "resource_type,resource_id,secondary_resource_id,subject_id,actor_id," +
            "organization_unit_id,sensitivity,metadata,retention_class,retention_expires_on");

        foreach (var r in rows)
        {
            var metadata = ParseMetadata(r.MetadataJson);
            var metadataCell = string.Join(";", metadata.Select(kv =>
                $"{kv.Key}={Convert.ToString(kv.Value, CultureInfo.InvariantCulture)}"));

            await writer.WriteLineAsync(string.Join(',',
                Csv(r.Id.ToString("D")),
                Csv(r.OccurredOn.ToString("O", CultureInfo.InvariantCulture)),
                Csv(r.IngestedOn.ToString("O", CultureInfo.InvariantCulture)),
                Csv(r.SourceService),
                Csv(r.SourceEventType),
                Csv(r.Action),
                Csv(r.Outcome),
                Csv(r.ResourceType),
                Csv(r.ResourceId.ToString("D")),
                Csv(r.SecondaryResourceId?.ToString("D")),
                Csv(r.SubjectId?.ToString("D")),
                Csv(r.ActorId?.ToString("D")),
                Csv(r.OrganizationUnitId?.ToString("D")),
                Csv(r.Sensitivity == Domain.AuditSensitivity.Sensitive ? "sensitive" : "normal"),
                Csv(metadataCell),
                Csv(r.RetentionClass),
                Csv(r.RetentionExpiresOn?.ToString("O", CultureInfo.InvariantCulture))));
        }
    }

    /// <summary>RFC 4180 quoting: wrap in quotes when needed and double inner quotes.</summary>
    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        return text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}

internal static class AuditJsonOptions
{
    internal static readonly JsonSerializerOptions Export = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };
}
