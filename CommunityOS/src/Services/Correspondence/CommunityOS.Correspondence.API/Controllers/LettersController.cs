using Asp.Versioning;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityOS.Correspondence.API.Extensions;
using CommunityOS.Correspondence.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Correspondence.API.Controllers;

/// <summary>
/// Letter endpoints (docs/api/correspondence.md). List responses are
/// metadata-only — <c>subject</c> is always <c>null</c>; full content requires
/// the single-letter read with the sensitive second pass when applicable.
/// </summary>
[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/correspondence/letters")]
public sealed class LettersController(IMediator mediator) : ControllerBase
{
    private const string CsvContentType = "text/csv; charset=utf-8";
    private const string NdjsonContentType = "application/x-ndjson; charset=utf-8";

    // ---- Query -------------------------------------------------------------

    [HttpGet]
    [ProducesResponseType<LetterPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LetterPageDto>> Query(
        [FromQuery] string? status,
        [FromQuery] string? category,
        [FromQuery] Guid? organizationUnitId,
        [FromQuery] DateTime? submittedFrom,
        [FromQuery] DateTime? submittedTo,
        [FromQuery] string? reference,
        [FromQuery] string order = "desc",
        [FromQuery] int? limit = null,
        [FromQuery] int offset = 0,
        [FromQuery] bool includeSensitive = false,
        CancellationToken ct = default)
    {
        var query = new QueryLetters(
            User.SubjectId(),
            new LetterQueryFilters(status, category, organizationUnitId, submittedFrom, submittedTo, reference),
            order, includeSensitive, limit, offset);
        return Ok(await mediator.Send(query, ct));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<LetterDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LetterDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetLetter(User.SubjectId(), id), ct));

    [HttpGet("{id:guid}/history")]
    [ProducesResponseType<IReadOnlyList<HistoryEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<HistoryEntryDto>>> History(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetLetterHistory(User.SubjectId(), id), ct));

    // ---- Lifecycle -----------------------------------------------------------

    public sealed record CreateLetterRequestBody(
        Guid OrganizationUnitId,
        string Category,
        string Sensitivity = "normal",
        string? Subject = null,
        string? Body = null,
        IReadOnlyList<RecipientInput>? Recipients = null,
        Guid? TemplateId = null,
        Guid? RelatedLetterId = null);

    [HttpPost]
    [ProducesResponseType<LetterDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LetterDto>> Create(
        [FromBody] CreateLetterRequestBody body,
        CancellationToken ct)
    {
        if (body is null || body.OrganizationUnitId == Guid.Empty)
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        var dto = await mediator.Send(new CreateLetterCommand(
            User.SubjectId(),
            body.Category,
            body.Sensitivity ?? "normal",
            body.Subject,
            body.Body,
            body.OrganizationUnitId,
            body.Recipients ?? [],
            body.TemplateId,
            body.RelatedLetterId), ct);

        return CreatedAtAction(nameof(Get), new { id = dto.Id, version = "1" }, dto);
    }

    public sealed record UpdateLetterRequestBody(string Subject, string Body, int ExpectedRevision);

    [HttpPut("{id:guid}")]
    [ProducesResponseType<LetterDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LetterDto>> Update(
        Guid id,
        [FromBody] UpdateLetterRequestBody requestBody,
        CancellationToken ct)
    {
        if (requestBody is null)
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        return Ok(await mediator.Send(new UpdateLetterContentCommand(
            User.SubjectId(), id, requestBody.Subject, requestBody.Body, requestBody.ExpectedRevision), ct));
    }

    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType<LetterDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LetterDto>> Confirm(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new ConfirmLetterCommand(User.SubjectId(), id), ct));

    [HttpPost("{id:guid}/unconfirm")]
    [ProducesResponseType<LetterDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LetterDto>> Unconfirm(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new UnconfirmLetterCommand(User.SubjectId(), id), ct));

    /// <summary>Accepted (202): the reference allocation and outbox-captured
    /// submission event commit atomically; dispatch remains blocked until the
    /// Documents materialization confirms.</summary>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType<SubmitLetterResult>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmitLetterResult>> Submit(Guid id, CancellationToken ct) =>
        Accepted(await mediator.Send(new SubmitLetterCommand(User.SubjectId(), id), ct));

    public sealed record CancelLetterRequestBody(string ReasonCode);

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<CancelLetterResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CancelLetterResult>> Cancel(
        Guid id,
        [FromBody] CancelLetterRequestBody body,
        CancellationToken ct)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.ReasonCode))
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        return Ok(await mediator.Send(new CancelLetterCommand(
            User.SubjectId(), id, body.ReasonCode), ct));
    }

    // ---- Dispatch and delivery (admin capability) ----------------------------

    public sealed record DispatchLetterRequestBody(string MethodCode);

    [HttpPost("{id:guid}/dispatch")]
    [ProducesResponseType<LetterDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LetterDto>> Dispatch(
        Guid id,
        [FromBody] DispatchLetterRequestBody body,
        CancellationToken ct)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.MethodCode))
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        return Ok(await mediator.Send(new DispatchLetterCommand(
            User.SubjectId(), id, body.MethodCode), ct));
    }

    public sealed record DeliveryOutcomeRequestBody(string Outcome, string? ReasonCode = null);

    [HttpPost("{id:guid}/delivery-outcome")]
    [ProducesResponseType<LetterDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LetterDto>> DeliveryOutcome(
        Guid id,
        [FromBody] DeliveryOutcomeRequestBody body,
        CancellationToken ct)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Outcome))
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        return Ok(await mediator.Send(new RecordDeliveryOutcomeCommand(
            User.SubjectId(), id, body.Outcome, body.ReasonCode), ct));
    }

    // ---- Export ---------------------------------------------------------------

    public sealed record ExportLettersRequestBody(
        string Format,
        LetterExportFiltersDto Filters,
        bool IncludeSensitive = false,
        int? MaxRows = null);

    public sealed record LetterExportFiltersDto(
        string? Status, string? Category, Guid? OrganizationUnitId,
        DateTime? SubmittedFrom, DateTime? SubmittedTo, string? Reference)
    {
        public LetterQueryFilters ToFilters() =>
            new(Status, Category, OrganizationUnitId, SubmittedFrom, SubmittedTo, Reference);
    }

    [HttpPost("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Export(
        [FromBody] ExportLettersRequestBody body,
        CancellationToken ct)
    {
        if (body is null || body.Filters is null)
        {
            return BadRequest(new { title = "Validation failed.", status = 400 });
        }

        var result = await mediator.Send(new ExportLettersCommand(
            User.SubjectId(),
            body.Format ?? "csv",
            body.Filters.ToFilters(),
            body.IncludeSensitive,
            body.MaxRows), ct);

        var (contentType, fileName) = result.Format == "ndjson"
            ? (NdjsonContentType, $"letters-export-{DateTime.UtcNow:yyyyMMddTHHmmssZ}.ndjson")
            : (CsvContentType, $"letters-export-{DateTime.UtcNow:yyyyMMddTHHmmssZ}.csv");

        Response.ContentType = contentType;
        Response.ContentLength = null;
        Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";

        await using var writer = new StreamWriter(Response.Body, new UTF8Encoding(false), leaveOpen: true);
        if (result.Format == "ndjson")
        {
            foreach (var row in result.Rows)
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(row, LetterJson.Options));
            }
        }
        else
        {
            await WriteCsvAsync(writer, result.Rows);
        }

        await writer.FlushAsync(ct);
        return new EmptyResult();
    }

    private static async Task WriteCsvAsync(StreamWriter writer, IReadOnlyList<LetterExportRow> rows)
    {
        await writer.WriteLineAsync("id,reference,organization_unit_id,category,sensitivity,status,recipient_count,person_recipient_count,unit_recipient_count,external_recipient_count,created_on,submitted_on");
        foreach (var row in rows)
        {
            var fields = new[]
            {
                row.Id.ToString("D"),
                row.Reference,
                row.OrganizationUnitId.ToString("D"),
                row.Category,
                row.Sensitivity,
                row.Status,
                row.RecipientCount.ToString(CultureInfo.InvariantCulture),
                row.PersonRecipientCount.ToString(CultureInfo.InvariantCulture),
                row.UnitRecipientCount.ToString(CultureInfo.InvariantCulture),
                row.ExternalRecipientCount.ToString(CultureInfo.InvariantCulture),
                row.CreatedOn.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                row.SubmittedOn?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
            };
            await writer.WriteLineAsync(string.Join(',', fields.Select(CsvField)));
        }
    }

    internal static string CsvField(string? value)
    {
        value ??= string.Empty;
        return value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }
}

internal static class LetterJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
