using CommunityOS.Audit.Application.Authorization;
using CommunityOS.Audit.Application.Permissions;
using CommunityOS.Audit.Domain;
using CommunityOS.Audit.Domain.Exceptions;
using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Options;

namespace CommunityOS.Audit.Application;

public sealed record AuditQueryFilters(
    string? SourceService,
    string? EventType,
    string? Action,
    string? ResourceType,
    Guid? ResourceId,
    Guid? SubjectId,
    Guid? ActorId,
    Guid? OrganizationUnitId,
    DateTime? OccurredFrom,
    DateTime? OccurredTo);

// ---- Queries / commands -------------------------------------------------

public sealed record AuditQuery(
    Guid ActorId,
    AuditQueryFilters Filters,
    string Order,
    bool IncludeSensitive,
    int? Limit,
    int Offset) : IRequest<AuditPageDto>;

public sealed record GetAuditEntry(Guid ActorId, Guid EntryId) : IRequest<AuditEntryDto>;

/// <summary>Exports at most MaxRows visible entries synchronously; the API
/// streams them in the requested format. The export itself is journaled
/// (ADR-027 decision 15).</summary>
public sealed record ExportAuditCommand(
    Guid ActorId,
    string Format,
    AuditQueryFilters Filters,
    bool IncludeSensitive,
    int? MaxRows) : IRequest<ExportResult>;

public sealed record ExportResult(IReadOnlyList<AuditEntryRow> Rows, string Format, int RowCount);

public sealed record PlaceHoldCommand(
    Guid ActorId,
    IReadOnlyList<Guid> EntryIds,
    string HoldType,
    string ReasonCode) : IRequest<IReadOnlyList<AuditHoldDto>>;

public sealed record ReleaseHoldCommand(Guid ActorId, Guid HoldId) : IRequest<AuditHoldDto>;

public sealed record PurgeExpiredCommand(Guid ActorId, int MaxBatchSize) : IRequest<PurgeResultDto>;

// ---- DTOs (API boundary — EF entities are never exposed) ----------------

public sealed record AuditEntryDto(
    Guid Id,
    DateTime OccurredOn,
    DateTime IngestedOn,
    string SourceService,
    string SourceEventType,
    string Action,
    string? Outcome,
    string ResourceType,
    Guid ResourceId,
    Guid? SecondaryResourceId,
    Guid? SubjectId,
    Guid? ActorId,
    Guid? OrganizationUnitId,
    string Sensitivity,
    Guid? CorrelationId,
    Guid? CausationId,
    IReadOnlyDictionary<string, string> Metadata,
    string RetentionClass,
    DateTime? RetentionExpiresOn);

public sealed record AuditPageDto(IReadOnlyList<AuditEntryDto> Items, int Limit, int Offset, int Returned);

public sealed record AuditHoldDto(
    Guid Id, Guid EntryId, string HoldType, Guid PlacedBy, DateTime PlacedOn, string ReasonCode);

public sealed record PurgeResultDto(int PurgedCount, Guid PurgeMarkerEntryId, int RemainingExpiredEstimate);

internal static class AuditEntryMapper
{
    public static AuditEntryDto ToDto(AuditEntryRow row) =>
        new(row.Id, row.OccurredOn, row.IngestedOn, row.SourceService, row.SourceEventType,
            row.Action, row.Outcome, row.ResourceType, row.ResourceId, row.SecondaryResourceId,
            row.SubjectId, row.ActorId, row.OrganizationUnitId,
            row.Sensitivity == AuditSensitivity.Sensitive ? "sensitive" : "normal",
            null, // reserved correlation/causation stay unpopulated at this gate (ADR-027 decision 7)
            null,
            string.IsNullOrEmpty(row.MetadataJson)
                ? AuditMetadata.Empty.Values
                : AuditMetadata.FromJson(row.MetadataJson).Values,
            row.RetentionClass, row.RetentionExpiresOn);
}

// ---- Handlers ------------------------------------------------------------

/// <summary>
/// Row-level visibility shared by the query and export handlers: the caller
/// must hold the read permission at ANY of the entry's authorization contexts
/// (its organization scope, or the global resource context for unscoped rows —
/// ADR-027 decision 11), and sensitive entries additionally require the
/// second-pass capability there.
/// </summary>
internal static class EntryVisibility
{
    public static async Task<bool> IsVisibleAsync(
        IAuthorizationEvaluator evaluator,
        Guid actorId,
        AuditEntryRow row,
        bool includeSensitive,
        CancellationToken ct)
    {
        var contexts = AuditAuthorization.ContextsFor(row);
        if (!await AnyAllowedAsync(evaluator, actorId, AuditPermissions.EntryRead, contexts, ct))
        {
            return false;
        }

        if (!row.IsSensitive) return true;
        if (!includeSensitive) return false;

        return await AnyAllowedAsync(evaluator, actorId, AuditPermissions.EntryReadSensitive, contexts, ct);
    }

    private static async Task<bool> AnyAllowedAsync(
        IAuthorizationEvaluator evaluator,
        Guid actorId,
        string permission,
        IReadOnlyList<AuthorizationContext> contexts,
        CancellationToken ct)
    {
        var requests = contexts
            .Select(scope => new AuthorizationRequest(actorId, permission, scope))
            .ToList();
        var decisions = await evaluator.EvaluateBatchAsync(requests, ct);
        return decisions.Any(d => d.Allowed);
    }
}

/// <summary>
/// Paged journal query (ADR-027 decision 13). A coarse gate first rejects
/// callers with no read grant at all (403); candidates are then filtered per
/// entry at the scope boundary BEFORE pagination. Sensitive entries are
/// absent entirely unless the caller opted in through
/// <c>includeSensitive</c>, which requires the second-pass capability. No
/// exhaustive total is exposed.
/// </summary>
public sealed class AuditQueryHandler(
    IAuditReader reader,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator,
    IOptions<AuditOptions> options)
    : IRequestHandler<AuditQuery, AuditPageDto>
{
    internal const int CandidateBatchSize = 200;

    public async Task<AuditPageDto> Handle(AuditQuery query, CancellationToken cancellationToken)
    {
        if (query.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");

        var limit = Math.Clamp(query.Limit ?? options.Value.DefaultPageSize, 1, options.Value.MaxPageSize);
        var offset = Math.Max(0, query.Offset);
        var ascending = query.Order.Equals("asc", StringComparison.OrdinalIgnoreCase);

        await guard.RequireAsync(
            query.ActorId, AuditPermissions.EntryRead, ScopeContext(query.Filters.OrganizationUnitId),
            cancellationToken);
        if (query.IncludeSensitive)
        {
            await guard.RequireAsync(
                query.ActorId, AuditPermissions.EntryReadSensitive, ScopeContext(query.Filters.OrganizationUnitId),
                cancellationToken);
        }

        // Walk deterministic candidate batches and collect visible entries
        // until the requested page window is filled or the store is exhausted.
        var visible = new List<AuditEntryRow>();
        var cursor = 0;
        while (visible.Count < offset + limit)
        {
            var candidates = await reader.QueryAsync(
                query.Filters, ascending, cursor, CandidateBatchSize, cancellationToken);
            if (candidates.Count == 0) break;

            foreach (var candidate in candidates)
            {
                if (await EntryVisibility.IsVisibleAsync(
                        evaluator, query.ActorId, candidate, query.IncludeSensitive, cancellationToken))
                {
                    visible.Add(candidate);
                    if (visible.Count == offset + limit) break;
                }
            }

            cursor += candidates.Count;
            if (candidates.Count < CandidateBatchSize) break;
        }

        var items = visible
            .Skip(offset)
            .Take(limit)
            .Select(AuditEntryMapper.ToDto)
            .ToList();

        return new(items, limit, offset, items.Count);
    }

    /// <summary>Coarse gate context: the requested organization scope when a
    /// unit filter was supplied, otherwise the global context.</summary>
    internal static AuthorizationContext ScopeContext(Guid? unit) =>
        unit is { } scope
            ? new AuthorizationContext(scope, AuditPermissions.ResourceType)
            : new AuthorizationContext(ResourceType: AuditPermissions.ResourceType);
}

/// <summary>
/// Single-entry read (ADR-027 decision 13). Missing and unauthorized entries
/// surface identically as 404: existence is never disclosed across the scope
/// or sensitivity boundaries.
/// </summary>
public sealed class GetAuditEntryHandler(
    IAuditReader reader,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator)
    : IRequestHandler<GetAuditEntry, AuditEntryDto>
{
    public async Task<AuditEntryDto> Handle(GetAuditEntry request, CancellationToken cancellationToken)
    {
        if (request.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");

        // Coarse capability gate: no read grant anywhere is a uniform 403;
        // everything past it is uniformly 404.
        await guard.RequireAsync(request.ActorId, AuditPermissions.EntryRead, ct: cancellationToken);

        var row = await reader.FindAsync(request.EntryId, cancellationToken);
        if (row is null ||
            !await EntryVisibility.IsVisibleAsync(evaluator, request.ActorId, row, includeSensitive: true, cancellationToken))
        {
            throw new AuditEntryNotFoundException();
        }

        return AuditEntryMapper.ToDto(row);
    }
}

/// <summary>
/// Synchronous bounded export (ADR-027 decision 13). Requires the export
/// capability (plus the sensitive second pass when opted in); collects up to
/// MaxRows authorized rows; journals the export itself — actor, filter
/// summary, row count and format, never row contents (decision 15).
/// </summary>
public sealed class ExportAuditHandler(
    IAuditReader reader,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator,
    IAuditJournal journal,
    IOptions<AuditOptions> options)
    : IRequestHandler<ExportAuditCommand, ExportResult>
{
    internal const int ExportBatchSize = 500;
    private static readonly string[] AllowedFormats = ["csv", "ndjson"];

    public async Task<ExportResult> Handle(ExportAuditCommand command, CancellationToken cancellationToken)
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
            command.ActorId, AuditPermissions.EntryExport,
            AuditQueryHandler.ScopeContext(command.Filters.OrganizationUnitId), cancellationToken);
        if (command.IncludeSensitive)
        {
            await guard.RequireAsync(
                command.ActorId, AuditPermissions.EntryReadSensitive,
                AuditQueryHandler.ScopeContext(command.Filters.OrganizationUnitId), cancellationToken);
        }

        var rows = new List<AuditEntryRow>(Math.Min(maxRows, ExportBatchSize));
        var cursor = 0;
        while (rows.Count < maxRows)
        {
            var candidates = await reader.QueryAsync(
                command.Filters, ascending: false, cursor, ExportBatchSize, cancellationToken);
            if (candidates.Count == 0) break;

            foreach (var candidate in candidates)
            {
                if (await EntryVisibility.IsVisibleAsync(
                        evaluator, command.ActorId, candidate, command.IncludeSensitive, cancellationToken))
                {
                    rows.Add(candidate);
                    if (rows.Count == maxRows) break;
                }
            }

            cursor += candidates.Count;
            if (candidates.Count < ExportBatchSize) break;
        }

        await JournalExportAsync(command, format, rows.Count, cancellationToken);
        return new(rows, format, rows.Count);
    }

    private async Task JournalExportAsync(
        ExportAuditCommand command, string format, int rowCount, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var exportId = Guid.NewGuid();
        var metadata = AuditMetadata.Create(new Dictionary<string, object?>
        {
            ["format"] = format,
            ["row_count"] = rowCount,
            ["filters"] = SummarizeFilters(command.Filters)
        });
        var hash = SourceEventHash.Compute(
            AuditSources.Audit, "AuditEntriesExported", AuditSources.ResourceTypes.AuditExport,
            exportId, null, now, $"{command.ActorId:D}|{rowCount}|{format}");
        var entry = AuditEntry.Create(
            AuditSources.Audit, "AuditEntriesExported", "audit-export", hash,
            AuditSources.ResourceTypes.AuditExport, exportId, now, now,
            AuditSensitivity.Normal, options.Value.JournalClass, metadata: metadata);
        await journal.AppendAsync(entry, ct);
    }

    /// <summary>Compact canonical filter summary for the export journal entry:
    /// ids/codes/timestamps only, bounded to the metadata value length.</summary>
    internal static string SummarizeFilters(AuditQueryFilters filters)
    {
        var parts = new List<string>(9);
        void Add(string code, object? value)
        {
            if (value is not null)
            {
                parts.Add($"{code}={value}");
            }
        }

        Add("svc", filters.SourceService);
        Add("evt", filters.EventType);
        Add("act", filters.Action);
        if (filters.ResourceType is not null)
        {
            parts.Add(filters.ResourceId is { } rid
                ? $"res={filters.ResourceType}:{rid:D}"
                : $"res={filters.ResourceType}");
        }

        Add("sub", filters.SubjectId?.ToString("D"));
        Add("actor", filters.ActorId?.ToString("D"));
        Add("unit", filters.OrganizationUnitId?.ToString("D"));
        Add("from", filters.OccurredFrom?.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        Add("to", filters.OccurredTo?.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        var summary = string.Join("|", parts);
        // An unfiltered export is legitimate; "none" keeps the journal value
        // within the metadata non-empty rule.
        if (summary.Length == 0) return "none";
        return summary.Length <= 200 ? summary : summary[..200];
    }
}

/// <summary>
/// Places legal/administrative holds (ADR-027 decisions 2 and 14). The
/// operation is atomic: every referenced entry must exist and be readable by
/// the caller (including the sensitivity second pass), no target may already
/// carry an active hold, and placement journals one audit-of-audit entry with
/// codes and counts only — never hold reasons as free text.
/// </summary>
public sealed class PlaceHoldHandler(
    IAuditReader reader,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator,
    IAuditJournal journal,
    IOptions<AuditOptions> options)
    : IRequestHandler<PlaceHoldCommand, IReadOnlyList<AuditHoldDto>>
{
    public async Task<IReadOnlyList<AuditHoldDto>> Handle(PlaceHoldCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, AuditPermissions.EntryAdmin, ct: cancellationToken);

        var ids = command.EntryIds.Distinct().ToList();
        var targets = await reader.FindRangeAsync(ids, cancellationToken);

        // Whole-batch atomicity: an unknown or invisible target fails the
        // entire request without disclosing which id was problematic.
        if (targets.Count != ids.Count ||
            !await AllReadableAsync(command.ActorId, targets, cancellationToken))
        {
            throw new AuditEntryNotFoundException();
        }

        foreach (var target in targets)
        {
            if (await reader.HasActiveHoldAsync(target.Id, cancellationToken))
            {
                throw new AuditConflictException("One or more entries already carry an active hold.");
            }
        }

        var now = DateTime.UtcNow;
        var holds = targets
            .Select(t => AuditEntryHold.Create(t.Id, command.HoldType, command.ReasonCode, command.ActorId, now))
            .ToList();

        var metadata = AuditMetadata.Create(new Dictionary<string, object?>
        {
            ["entry_count"] = holds.Count,
            ["hold_type"] = command.HoldType,
            ["reason_code"] = command.ReasonCode
        });
        var hash = SourceEventHash.Compute(
            AuditSources.Audit, "AuditEntryHoldPlaced", AuditSources.ResourceTypes.AuditHold,
            holds[0].Id, null, now, $"{command.ActorId:D}|{string.Join(',', ids.Select(i => i.ToString("D")))}");
        var journalEntry = AuditEntry.Create(
            AuditSources.Audit, "AuditEntryHoldPlaced", "audit-hold-placed", hash,
            AuditSources.ResourceTypes.AuditHold, holds[0].Id, now, now,
            AuditSensitivity.Normal, options.Value.JournalClass, metadata: metadata);

        await journal.AppendWithHoldsAsync(journalEntry, holds, cancellationToken);

        return holds
            .Select(h => new AuditHoldDto(h.Id, h.EntryId, h.HoldType, h.PlacedBy, h.PlacedOn, h.ReasonCode))
            .ToList();
    }

    private async Task<bool> AllReadableAsync(Guid actorId, IReadOnlyList<AuditEntryRow> rows, CancellationToken ct)
    {
        foreach (var row in rows)
        {
            if (!await EntryVisibility.IsVisibleAsync(evaluator, actorId, row, includeSensitive: true, ct))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Releases an active hold (ADR-027 decision 14). Releasing an unknown hold
/// is 404; releasing an already-released hold is a 409 conflict. The release
/// is journaled with codes only.
/// </summary>
public sealed class ReleaseHoldHandler(
    IAuditReader reader,
    AuthorizationGuard guard,
    IAuditJournal journal,
    IOptions<AuditOptions> options)
    : IRequestHandler<ReleaseHoldCommand, AuditHoldDto>
{
    public async Task<AuditHoldDto> Handle(ReleaseHoldCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, AuditPermissions.EntryAdmin, ct: cancellationToken);

        var hold = await reader.FindHoldAsync(command.HoldId, cancellationToken);
        if (hold is null) throw new AuditEntryNotFoundException();
        if (!hold.IsActive)
        {
            throw new AuditConflictException("The hold has already been released.");
        }

        var now = DateTime.UtcNow;
        hold.Release(command.ActorId, now);

        var metadata = AuditMetadata.Create(new Dictionary<string, object?>
        {
            ["hold_type"] = hold.HoldType,
            ["reason_code"] = hold.ReasonCode
        });
        var hash = SourceEventHash.Compute(
            AuditSources.Audit, "AuditEntryHoldReleased", AuditSources.ResourceTypes.AuditHold,
            hold.Id, hold.EntryId, now, $"{command.ActorId:D}|release");
        var journalEntry = AuditEntry.Create(
            AuditSources.Audit, "AuditEntryHoldReleased", "audit-hold-released", hash,
            AuditSources.ResourceTypes.AuditHold, hold.Id, now, now,
            AuditSensitivity.Normal, options.Value.JournalClass,
            secondaryResourceId: hold.EntryId, metadata: metadata);

        await journal.UpdateHoldAsync(hold, journalEntry, cancellationToken);

        return new AuditHoldDto(hold.Id, hold.EntryId, hold.HoldType, hold.PlacedBy, hold.PlacedOn, hold.ReasonCode);
    }
}

/// <summary>
/// Executes one retention-purge batch (ADR-027 decision 14). Expiry alone
/// never deletes: this administrative step selects up to the bounded batch of
/// expired unheld entries, writes its purge-marker entry inside the same
/// transaction under the database trigger guard, and then removes exactly that
/// batch. Active holds always override expiry.
/// </summary>
public sealed class PurgeExpiredHandler(
    IAuditJournal journal,
    AuthorizationGuard guard,
    IOptions<AuditOptions> options)
    : IRequestHandler<PurgeExpiredCommand, PurgeResultDto>
{
    public async Task<PurgeResultDto> Handle(PurgeExpiredCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, AuditPermissions.EntryAdmin, ct: cancellationToken);

        var size = Math.Clamp(
            command.MaxBatchSize == 0 ? options.Value.PurgeDefaultBatchSize : command.MaxBatchSize,
            1, options.Value.PurgeMaxBatchSize);

        var now = DateTime.UtcNow;
        PurgeMarkerHolder markerHolder = new();
        var result = await journal.PurgeExpiredBatchAsync(size, BuildMarker(markerHolder, now, command.ActorId), now, cancellationToken);
        return new(result.PurgedCount, markerHolder.Entry!.Id, result.RemainingExpired);
    }

    private Func<int, IReadOnlyList<string>, AuditEntry> BuildMarker(
        PurgeMarkerHolder holder, DateTime now, Guid actorId) =>
        (purgedCount, retentionClasses) =>
        {
            var metadata = AuditMetadata.Create(new Dictionary<string, object?>
            {
                ["purged_count"] = purgedCount,
                ["retention_classes"] =
                    string.Join(",", retentionClasses)[..Math.Min(string.Join(",", retentionClasses).Length, 200)]
            });
            var markerId = Guid.NewGuid();
            var hash = SourceEventHash.Compute(
                AuditSources.Audit, "AuditEntriesPurged", AuditSources.ResourceTypes.AuditEntry,
                markerId, null, now, $"purge|{actorId:D}|{purgedCount}");
            holder.Entry = AuditEntry.Create(
                AuditSources.Audit, "AuditEntriesPurged", "audit-purge", hash,
                AuditSources.ResourceTypes.AuditEntry, markerId, now, now,
                AuditSensitivity.Normal, OptionsJournalClass(), metadata: metadata);
            return holder.Entry;
        };

    private string OptionsJournalClass() => options.Value.JournalClass;

    private sealed class PurgeMarkerHolder
    {
        public AuditEntry? Entry { get; set; }
    }
}
