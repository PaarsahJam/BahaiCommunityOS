using CommunityOS.Correspondence.Application;
using CommunityOS.Correspondence.Domain;
using CommunityOS.Correspondence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Correspondence.Infrastructure;

/// <summary>
/// Read-side implementation. All queries order by submission/creation time
/// with an id tiebreak so paging is deterministic; authorization filtering
/// happens in the application layer on the returned rows (ADR-028 decision
/// 15). Projections are metadata-only — subjects, bodies and display lines
/// never leave the store on the list/export paths.
/// </summary>
public sealed class LetterReader(CorrespondenceDbContext db) : ILetterReader
{
    public async Task<IReadOnlyList<LetterSummaryRow>> QueryAsync(
        LetterQueryFilters filters, bool ascending, int offset, int maxRows, CancellationToken ct)
    {
        var query = BuildFilterQuery(filters);

        // Submission time when allocated, creation time otherwise — the
        // ratified deterministic ordering key.
        query = ascending
            ? query.OrderBy(l => l.SubmittedOn ?? l.CreatedOn).ThenBy(l => l.Id)
            : query.OrderByDescending(l => l.SubmittedOn ?? l.CreatedOn).ThenByDescending(l => l.Id);

        var rows = await query
            .Skip(offset)
            .Take(maxRows)
            .Select(l => new LetterSummaryRow(
                l.Id, l.OrganizationUnitId, l.CategoryCode, l.LetterYear, l.LetterSequence,
                l.Status.ToString().ToLowerInvariant(),
                l.Sensitivity == LetterSensitivity.Sensitive ? "sensitive" : "normal",
                db.LetterHolds.Any(h => h.LetterId == l.Id && h.ReleasedOn == null),
                l.Recipients.Count,
                l.Recipients.Count(r => r.Kind == RecipientKind.Person),
                l.Recipients.Count(r => r.Kind == RecipientKind.Unit),
                l.Recipients.Count(r => r.Kind == RecipientKind.External),
                l.SubjectPersonId, l.CreatedOn, l.SubmittedOn))
            .ToListAsync(ct);
        return rows;
    }

    public async Task<LetterSummaryRow?> FindSummaryAsync(Guid id, CancellationToken ct)
    {
        return await db.Letters
            .Where(l => l.Id == id)
            .Select(l => new LetterSummaryRow(
                l.Id, l.OrganizationUnitId, l.CategoryCode, l.LetterYear, l.LetterSequence,
                l.Status.ToString().ToLowerInvariant(),
                l.Sensitivity == LetterSensitivity.Sensitive ? "sensitive" : "normal",
                db.LetterHolds.Any(h => h.LetterId == l.Id && h.ReleasedOn == null),
                l.Recipients.Count,
                l.Recipients.Count(r => r.Kind == RecipientKind.Person),
                l.Recipients.Count(r => r.Kind == RecipientKind.Unit),
                l.Recipients.Count(r => r.Kind == RecipientKind.External),
                l.SubjectPersonId, l.CreatedOn, l.SubmittedOn))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<LetterDetailRow?> FindDetailAsync(Guid id, CancellationToken ct)
    {
        var letter = await db.Letters
            .Include(l => l.Recipients)
            .Include(l => l.DocumentLinks)
            .Include(l => l.Attachments)
            .FirstOrDefaultAsync(l => l.Id == id, ct);
        if (letter is null) return null;

        var isHeld = await db.LetterHolds
            .AnyAsync(h => h.LetterId == id && h.ReleasedOn == null, ct);

        return new LetterDetailRow(
            letter.Id, letter.OrganizationUnitId, letter.CategoryCode,
            letter.Subject, letter.Body,
            letter.Sensitivity == LetterSensitivity.Sensitive ? "sensitive" : "normal",
            letter.Status.ToString().ToLowerInvariant(),
            letter.Revision, letter.LetterYear, letter.LetterSequence,
            letter.TemplateId, letter.TemplateCode, letter.RelatedLetterId, letter.SubjectPersonId,
            letter.CreatedBy, letter.CreatedOn, letter.UpdatedOn,
            letter.SubmittedBy, letter.SubmittedOn, letter.MaterializedOn,
            letter.DispatchedOn, letter.DeliveredOn, letter.DeliveryFailureReasonCode,
            letter.CancelledBy, letter.CancelledOn, letter.CancellationReasonCode,
            letter.RetentionClass, letter.RetentionExpiresOn, isHeld,
            letter.Recipients
                .Select(r => new LetterRecipientRow(
                    r.Id, r.Kind.ToString().ToLowerInvariant(), r.PersonId, r.UnitId, r.DisplayLine))
                .ToList(),
            letter.DocumentLinks
                .Select(d => new LetterDocumentLinkRow(d.DocumentId, d.VersionNumber, d.ContentHash, d.MaterializedOn))
                .ToList(),
            letter.Attachments
                .Select(a => new LetterAttachmentRow(a.Id, a.DocumentId, a.ReferenceType, a.AddedOn))
                .ToList());
    }

    public async Task<IReadOnlyList<LetterSummaryRow>> FindRangeSummariesAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];

        var rows = await db.Letters
            .Where(l => ids.Contains(l.Id))
            .OrderBy(l => l.SubmittedOn ?? l.CreatedOn).ThenBy(l => l.Id)
            .Select(l => new LetterSummaryRow(
                l.Id, l.OrganizationUnitId, l.CategoryCode, l.LetterYear, l.LetterSequence,
                l.Status.ToString().ToLowerInvariant(),
                l.Sensitivity == LetterSensitivity.Sensitive ? "sensitive" : "normal",
                db.LetterHolds.Any(h => h.LetterId == l.Id && h.ReleasedOn == null),
                l.Recipients.Count,
                l.Recipients.Count(r => r.Kind == RecipientKind.Person),
                l.Recipients.Count(r => r.Kind == RecipientKind.Unit),
                l.Recipients.Count(r => r.Kind == RecipientKind.External),
                l.SubjectPersonId, l.CreatedOn, l.SubmittedOn))
            .ToListAsync(ct);
        return rows;
    }

    public async Task<IReadOnlyList<HistoryRow>> GetHistoryAsync(Guid letterId, CancellationToken ct)
    {
        var rows = await db.Set<LetterStatusHistory>()
            .Where(h => h.LetterId == letterId)
            .OrderBy(h => h.OccurredOn).ThenBy(h => h.Id)
            .Select(h => new HistoryRow(
                h.Id, h.FromStatus, h.ToStatus, h.Cause, h.ActorId, h.ReasonCode, h.OccurredOn))
            .ToListAsync(ct);
        return rows;
    }

    public Task<bool> HasActiveHoldAsync(Guid letterId, CancellationToken ct) =>
        db.LetterHolds.AnyAsync(h => h.LetterId == letterId && h.ReleasedOn == null, ct);

    public Task<LetterHold?> FindHoldAsync(Guid holdId, CancellationToken ct) =>
        db.LetterHolds.FirstOrDefaultAsync(h => h.Id == holdId, ct);

    public Task<int> CountExpiredUnheldAsync(DateTime asOf, CancellationToken ct) =>
        db.Letters
            .Where(l => l.RetentionExpiresOn != null && l.RetentionExpiresOn <= asOf)
            .Where(l => !db.LetterHolds.Any(h => h.LetterId == l.Id && h.ReleasedOn == null))
            .CountAsync(ct);

    public async Task<IReadOnlyList<TemplateRow>> ListActiveTemplatesAsync(CancellationToken ct)
    {
        var rows = await db.Templates
            .Where(t => t.IsActive)
            .OrderBy(t => t.Code)
            .Select(t => new TemplateRow(t.Id, t.Code, t.Title, t.CategoryCode, t.CreatedOn, t.UpdatedOn, t.IsActive))
            .ToListAsync(ct);
        return rows;
    }

    public Task<Template?> FindTemplateAsync(Guid id, CancellationToken ct) =>
        db.Templates.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<bool> TemplateCodeExistsAsync(string code, CancellationToken ct) =>
        db.Templates.AnyAsync(t => t.Code == code, ct);

    public async Task<IReadOnlyList<StuckSubmissionRow>> ListStuckSubmissionsAsync(DateTime olderThan, int maxRows, CancellationToken ct)
    {
        var rows = await db.Letters
            .Where(l => l.Status == LetterStatus.Submitted && l.SubmittedOn != null && l.SubmittedOn <= olderThan)
            .OrderBy(l => l.SubmittedOn).ThenBy(l => l.Id)
            .Take(maxRows)
            .Select(l => new StuckSubmissionRow(l.Id, l.OrganizationUnitId, l.SubmittedOn!.Value))
            .ToListAsync(ct);
        return rows;
    }

    internal IQueryable<Letter> BuildFilterQuery(LetterQueryFilters filters)
    {
        var query = db.Letters.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.Status))
        {
            var status = Enum.Parse<LetterStatus>(filters.Status.Trim(), ignoreCase: true);
            query = query.Where(l => l.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filters.Category))
        {
            query = query.Where(l => l.CategoryCode == filters.Category.Trim());
        }

        if (filters.OrganizationUnitId is { } unitId)
        {
            query = query.Where(l => l.OrganizationUnitId == unitId);
        }

        if (filters.SubmittedFrom is { } from)
        {
            query = query.Where(l => l.SubmittedOn != null && l.SubmittedOn >= from.ToUniversalTime());
        }

        if (filters.SubmittedTo is { } to)
        {
            query = query.Where(l => l.SubmittedOn != null && l.SubmittedOn <= to.ToUniversalTime());
        }

        if (ParseReference(filters.Reference) is { } reference)
        {
            // Exact reference-number match only (year + sequence).
            query = query.Where(l =>
                l.LetterYear == reference.Year &&
                l.LetterSequence == reference.Sequence);
        }

        return query;
    }

    /// <summary>Parses a ratified <c>{year}-{sequence}</c> exact-match
    /// reference (there is no subject or body search).</summary>
    internal static (int Year, int Sequence)? ParseReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;
        var parts = reference.Trim().Split('-', 2);
        if (parts.Length != 2 ||
            parts[1].Length is < 1 or > 6 ||
            !int.TryParse(parts[0], System.Globalization.NumberStyles.None, null, out var year) ||
            !int.TryParse(parts[1], System.Globalization.NumberStyles.None, null, out var sequence) ||
            sequence < 0)
        {
            throw new ArgumentException("Reference must match '{year}-{sequence}' (e.g. 2026-00042).");
        }

        return (year, sequence);
    }
}
