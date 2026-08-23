using CommunityOS.Correspondence.Application.Authorization;
using CommunityOS.Correspondence.Application.Permissions;
using CommunityOS.Correspondence.Domain;
using CommunityOS.Correspondence.Domain.Exceptions;
using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Options;

namespace CommunityOS.Correspondence.Application;

// ---- API DTOs (EF entities are never exposed) ------------------------------

public sealed record LetterSummaryDto(
    Guid Id,
    string? Reference,
    string? Subject,
    string Category,
    string Sensitivity,
    string Status,
    int RecipientCount,
    int PersonRecipientCount,
    int UnitRecipientCount,
    int ExternalRecipientCount,
    Guid OrganizationUnitId,
    DateTime CreatedOn,
    DateTime? SubmittedOn,
    bool IsHeld);

public sealed record LetterRecipientDto(
    Guid Id, string Kind, Guid? PersonId, Guid? UnitId, string? DisplayLine);

public sealed record LetterDto(
    Guid Id,
    string? Reference,
    string Subject,
    string Body,
    string Category,
    string Sensitivity,
    string Status,
    int Revision,
    Guid OrganizationUnitId,
    IReadOnlyList<LetterRecipientDto> Recipients,
    Guid? DocumentReference,
    Guid? RelatedLetterId,
    bool IsHeld,
    Guid CreatedBy,
    DateTime CreatedOn,
    DateTime UpdatedOn,
    DateTime? SubmittedOn,
    DateTime? MaterializedOn,
    DateTime? DispatchedOn,
    DateTime? DeliveredOn,
    DateTime? CancelledOn,
    string RetentionClass,
    DateTime? RetentionExpiresOn);

public sealed record HistoryEntryDto(
    Guid Id, string FromStatus, string ToStatus, string Cause,
    Guid? ActorId, string? ReasonCode, DateTime OccurredOn);

public sealed record LetterPageDto(
    IReadOnlyList<LetterSummaryDto> Items, int Limit, int Offset, int Returned);

internal static class LetterFormatting
{
    public static string Status(LetterStatus status) => status.ToString().ToLowerInvariant();

    public static LetterStatus? ParseStatus(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.TryParse<LetterStatus>(value.Trim(), ignoreCase: true, out var parsed)
                ? parsed
                : throw new ArgumentException($"Unknown status '{value}'.", nameof(value));

    public static string Sensitivity(LetterSensitivity sensitivity) =>
        sensitivity == LetterSensitivity.Sensitive ? "sensitive" : "normal";

    public static string Kind(RecipientKind kind) => kind.ToString().ToLowerInvariant();

    public static string Cause(HistoryCause cause) =>
        cause == HistoryCause.PurgeMarker ? "purge-marker" : cause.ToString().ToLowerInvariant();
}

internal static class LetterMapper
{
    public static LetterSummaryDto ToSummaryDto(LetterSummaryRow row) =>
        new(row.Id, row.Reference, null, row.CategoryCode,
            LetterFormatting.Sensitivity(ParseSensitivity(row.Sensitivity)),
            row.Status.ToLowerInvariant(),
            row.RecipientCount, row.PersonRecipientCount, row.UnitRecipientCount, row.ExternalRecipientCount,
            row.OrganizationUnitId, row.CreatedOn, row.SubmittedOn, row.IsHeld);

    private static LetterSensitivity ParseSensitivity(string value) =>
        value == "sensitive" ? LetterSensitivity.Sensitive : LetterSensitivity.Normal;

    public static LetterDto ToDetailDto(LetterDetailRow row) =>
        new(row.Id, row.Reference, row.Subject, row.Body, row.CategoryCode,
            LetterFormatting.Sensitivity(ParseSensitivity(row.Sensitivity)),
            row.Status.ToLowerInvariant(), row.Revision, row.OrganizationUnitId,
            row.Recipients
                .Select(r => new LetterRecipientDto(r.Id, r.Kind, r.PersonId, r.UnitId, r.DisplayLine))
                .ToList(),
            row.DocumentLinks.OrderBy(l => l.MaterializedOn).FirstOrDefault()?.DocumentId,
            row.RelatedLetterId, row.IsHeld, row.CreatedBy, row.CreatedOn, row.UpdatedOn,
            row.SubmittedOn, row.MaterializedOn, row.DispatchedOn, row.DeliveredOn, row.CancelledOn,
            row.RetentionClass, row.RetentionExpiresOn);
}

/// <summary>
/// Row-level visibility shared by query/export/hold flows (ADR-028 decision
/// 15): the caller must hold the read permission at the letter's organization
/// scope, and sensitive letters additionally require the second-pass capability
/// there.
/// </summary>
internal static class LetterVisibility
{
    public static async Task<bool> IsVisibleAsync(
        IAuthorizationEvaluator evaluator,
        Guid actorId,
        LetterSummaryRow row,
        bool includeSensitive,
        CancellationToken ct)
    {
        var context = LetterAuthorization.ContextFor(row.Id, row.OrganizationUnitId);
        if (!await AllowedAsync(evaluator, actorId, LetterPermissions.LetterRead, context, ct))
        {
            return false;
        }

        if (row.Sensitivity != "sensitive") return true;
        if (!includeSensitive) return false;

        return await AllowedAsync(evaluator, actorId, LetterPermissions.LetterReadSensitive, context, ct);
    }

    public static async Task<bool> CanActAsync(
        IAuthorizationEvaluator evaluator,
        Guid actorId,
        string permission,
        Guid letterId,
        Guid organizationUnitId,
        CancellationToken ct)
    {
        var context = LetterAuthorization.ContextFor(letterId, organizationUnitId);
        return await AllowedAsync(evaluator, actorId, permission, context, ct);
    }

    private static async Task<bool> AllowedAsync(
        IAuthorizationEvaluator evaluator,
        Guid actorId,
        string permission,
        AuthorizationContext context,
        CancellationToken ct)
    {
        var decisions = await evaluator.EvaluateBatchAsync(
                [new AuthorizationRequest(actorId, permission, context)], ct);
        return decisions is { Count: > 0 } && decisions[0].Allowed;
    }
}

// ---- Queries -----------------------------------------------------------------

public sealed record QueryLetters(
    Guid ActorId,
    LetterQueryFilters Filters,
    string Order,
    bool IncludeSensitive,
    int? Limit,
    int Offset) : IRequest<LetterPageDto>;

public sealed class QueryLettersHandler(
    ILetterReader reader,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator,
    IOptions<CorrespondenceOptions> options)
    : IRequestHandler<QueryLetters, LetterPageDto>
{
    internal const int CandidateBatchSize = 200;

    public async Task<LetterPageDto> Handle(QueryLetters query, CancellationToken cancellationToken)
    {
        if (query.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");

        var limit = Math.Clamp(query.Limit ?? options.Value.DefaultPageSize, 1, options.Value.MaxPageSize);
        var offset = Math.Max(0, query.Offset);
        var ascending = query.Order.Equals("asc", StringComparison.OrdinalIgnoreCase);

        await guard.RequireAsync(
            query.ActorId, LetterPermissions.LetterRead, ScopeContext(query.Filters.OrganizationUnitId),
            cancellationToken);
        if (query.IncludeSensitive)
        {
            await guard.RequireAsync(
                query.ActorId, LetterPermissions.LetterReadSensitive,
                ScopeContext(query.Filters.OrganizationUnitId), cancellationToken);
        }

        var visible = new List<LetterSummaryRow>();
        var cursor = 0;
        while (visible.Count < offset + limit)
        {
            var candidates = await reader.QueryAsync(
                query.Filters, ascending, cursor, CandidateBatchSize, cancellationToken);
            if (candidates.Count == 0) break;

            foreach (var candidate in candidates)
            {
                if (await LetterVisibility.IsVisibleAsync(
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
            .Select(LetterMapper.ToSummaryDto)
            .ToList();

        return new(items, limit, offset, items.Count);
    }

    internal static AuthorizationContext ScopeContext(Guid? unit) =>
        unit is { } scope
            ? new AuthorizationContext(scope, LetterPermissions.ResourceType)
            : new AuthorizationContext(ResourceType: LetterPermissions.ResourceType);
}

public sealed record GetLetter(Guid ActorId, Guid LetterId) : IRequest<LetterDto>;

/// <summary>Single letter read. Missing and unauthorized letters surface
/// identically as 404 (uniform anti-enumeration); sensitive letters require
/// the second pass.</summary>
public sealed class GetLetterHandler(
    ILetterReader reader,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator)
    : IRequestHandler<GetLetter, LetterDto>
{
    public async Task<LetterDto> Handle(GetLetter request, CancellationToken cancellationToken)
    {
        if (request.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");

        await guard.RequireAsync(request.ActorId, LetterPermissions.LetterRead, ct: cancellationToken);

        var row = await reader.FindDetailAsync(request.LetterId, cancellationToken);
        if (row is null ||
            !await LetterVisibility.CanActAsync(evaluator, request.ActorId,
                LetterPermissions.LetterRead, row.Id, row.OrganizationUnitId, cancellationToken))
        {
            throw new LetterNotFoundException();
        }

        if (row.Sensitivity == "sensitive" &&
            !await LetterVisibility.CanActAsync(evaluator, request.ActorId,
                LetterPermissions.LetterReadSensitive, row.Id, row.OrganizationUnitId, cancellationToken))
        {
            throw new LetterNotFoundException();
        }

        return LetterMapper.ToDetailDto(row);
    }
}

public sealed record GetLetterHistory(Guid ActorId, Guid LetterId) : IRequest<IReadOnlyList<HistoryEntryDto>>;

public sealed class GetLetterHistoryHandler(
    ILetterReader reader,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator)
    : IRequestHandler<GetLetterHistory, IReadOnlyList<HistoryEntryDto>>
{
    public async Task<IReadOnlyList<HistoryEntryDto>> Handle(GetLetterHistory request, CancellationToken cancellationToken)
    {
        if (request.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");

        await guard.RequireAsync(request.ActorId, LetterPermissions.LetterRead, ct: cancellationToken);

        var summary = await reader.FindSummaryAsync(request.LetterId, cancellationToken);
        if (summary is null ||
            !await LetterVisibility.CanActAsync(evaluator, request.ActorId,
                LetterPermissions.LetterRead, summary.Id, summary.OrganizationUnitId, cancellationToken))
        {
            throw new LetterNotFoundException();
        }

        if (summary.Sensitivity == "sensitive" &&
            !await LetterVisibility.CanActAsync(evaluator, request.ActorId,
                LetterPermissions.LetterReadSensitive, summary.Id, summary.OrganizationUnitId, cancellationToken))
        {
            throw new LetterNotFoundException();
        }

        var history = await reader.GetHistoryAsync(request.LetterId, cancellationToken);
        return history
            .Select(h => new HistoryEntryDto(
                h.Id, h.FromStatus.ToString().ToLowerInvariant(), h.ToStatus.ToString().ToLowerInvariant(),
                LetterFormatting.Cause(h.Cause), h.ActorId, h.ReasonCode, h.OccurredOn))
            .ToList();
    }
}
