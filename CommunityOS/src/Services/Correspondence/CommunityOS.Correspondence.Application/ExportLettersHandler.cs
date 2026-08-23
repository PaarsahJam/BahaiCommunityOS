using System.Globalization;
using CommunityOS.Correspondence.Application.Permissions;
using CommunityOS.Correspondence.Domain;
using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Options;

namespace CommunityOS.Correspondence.Application;

/// <summary>One metadata-index export row. Bodies, subjects and display lines
/// are never exported (ADR-028 privacy posture).</summary>
public sealed record LetterExportRow(
    Guid Id,
    string? Reference,
    Guid OrganizationUnitId,
    string Category,
    string Sensitivity,
    string Status,
    int RecipientCount,
    int PersonRecipientCount,
    int UnitRecipientCount,
    int ExternalRecipientCount,
    DateTime CreatedOn,
    DateTime? SubmittedOn);

public sealed record ExportLettersResult(
    IReadOnlyList<LetterExportRow> Rows, string Format, int RowCount);

public sealed record ExportLettersCommand(
    Guid ActorId,
    string Format,
    LetterQueryFilters Filters,
    bool IncludeSensitive,
    int? MaxRows) : IRequest<ExportLettersResult>;

/// <summary>
/// Capped synchronous export of the letter metadata index (ADR-028). Requires
/// the export capability plus the sensitive second pass when opted in; the
/// export itself is journaled internally in <c>export_activity</c> — actor,
/// filter summary, row count and format, never row contents.
/// </summary>
public sealed class ExportLettersHandler(
    ILetterReader reader,
    ILetterJournal journal,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator,
    IOptions<CorrespondenceOptions> options)
    : IRequestHandler<ExportLettersCommand, ExportLettersResult>
{
    internal const int ExportBatchSize = 500;
    private static readonly string[] AllowedFormats = ["csv", "ndjson"];

    public async Task<ExportLettersResult> Handle(ExportLettersCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");

        var format = command.Format.Trim().ToLowerInvariant();
        if (!AllowedFormats.Contains(format))
        {
            throw new ArgumentException("Export format must be 'csv' or 'ndjson'.", nameof(command));
        }

        var cap = options.Value.ExportMaxRows;
        if (command.MaxRows is { } requested && (requested < 1 || requested > cap))
        {
            throw new ArgumentException($"maxRows must be between 1 and {cap}.", nameof(command));
        }

        var maxRows = command.MaxRows ?? cap;

        await guard.RequireAsync(
            command.ActorId, LetterPermissions.LetterExport,
            QueryLettersHandler.ScopeContext(command.Filters.OrganizationUnitId), cancellationToken);
        if (command.IncludeSensitive)
        {
            await guard.RequireAsync(
                command.ActorId, LetterPermissions.LetterReadSensitive,
                QueryLettersHandler.ScopeContext(command.Filters.OrganizationUnitId), cancellationToken);
        }

        var rows = new List<LetterSummaryRow>(Math.Min(maxRows, ExportBatchSize));
        var cursor = 0;
        while (rows.Count < maxRows)
        {
            var candidates = await reader.QueryAsync(
                command.Filters, ascending: false, cursor, ExportBatchSize, cancellationToken);
            if (candidates.Count == 0) break;

            foreach (var candidate in candidates)
            {
                if (await LetterVisibility.IsVisibleAsync(
                        evaluator, command.ActorId, candidate, command.IncludeSensitive, cancellationToken))
                {
                    rows.Add(candidate);
                    if (rows.Count == maxRows) break;
                }
            }

            cursor += candidates.Count;
            if (candidates.Count < ExportBatchSize) break;
        }

        var exportRows = rows
            .Select(r => new LetterExportRow(
                r.Id, r.Reference, r.OrganizationUnitId, r.CategoryCode,
                r.Sensitivity, r.Status, r.RecipientCount,
                r.PersonRecipientCount, r.UnitRecipientCount, r.ExternalRecipientCount,
                r.CreatedOn, r.SubmittedOn))
            .ToList();

        await journal.SaveExportActivityAsync(
            ExportActivity.Create(
                command.ActorId, format, SummarizeFilters(command.Filters),
                exportRows.Count, command.IncludeSensitive, DateTime.UtcNow),
            cancellationToken);

        return new(exportRows, format, exportRows.Count);
    }

    /// <summary>Compact canonical filter summary: ids/codes/timestamps only.</summary>
    internal static string SummarizeFilters(LetterQueryFilters filters)
    {
        var parts = new List<string>(6);
        void Add(string code, object? value)
        {
            if (value is not null)
            {
                parts.Add($"{code}={value}");
            }
        }

        Add("status", filters.Status);
        Add("cat", filters.Category);
        Add("unit", filters.OrganizationUnitId?.ToString("D"));
        Add("from", filters.SubmittedFrom?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Add("to", filters.SubmittedTo?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Add("ref", filters.Reference);

        var summary = string.Join("|", parts);
        if (summary.Length == 0) return "none";
        return summary.Length <= 200 ? summary : summary[..200];
    }
}
