using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Records.Application.Authorization;
using CommunityOS.Records.Application.DTOs;
using CommunityOS.Records.Application.Permissions;
using CommunityOS.Records.Domain.Aggregates;
using CommunityOS.Records.Domain.Enumerations;
using CommunityOS.Records.Domain.Exceptions;
using CommunityOS.Records.Domain.Repositories;
using MediatR;

namespace CommunityOS.Records.Application.Queries;

public sealed record ListRecordsQuery(
    Guid ActorId,
    string? Category,
    string? Status,
    Guid? OrganizationUnitId,
    Guid? PersonId,
    Guid? HouseholdId,
    bool? IsSensitive,
    string? Query) : IRequest<IReadOnlyList<RecordSummaryDto>>;

internal sealed class ListRecordsQueryHandler(
    IRecordRepository records,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator)
    : IRequestHandler<ListRecordsQuery, IReadOnlyList<RecordSummaryDto>>
{
    public async Task<IReadOnlyList<RecordSummaryDto>> Handle(ListRecordsQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, RecordsPermissions.RecordRead,
            query.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "record")
                : new AuthorizationContext(ResourceType: "record"), ct);

        var candidates = query.OrganizationUnitId is { } filterUnit
            ? await records.ListByOrganizationUnitAsync(filterUnit, ct)
            : await records.ListAsync(ct);

        var filtered = candidates
            .Where(r => query.Category is null || string.Equals(r.Category, query.Category, StringComparison.OrdinalIgnoreCase))
            .Where(r => query.Status is null || string.Equals(r.Status.Name, query.Status, StringComparison.OrdinalIgnoreCase))
            .Where(r => query.IsSensitive is null || r.Classification.IsSensitive == query.IsSensitive)
            .Where(r => query.PersonId is null ||
                (string.Equals(r.SubjectType, RecordSubjectTypes.Person, StringComparison.OrdinalIgnoreCase) &&
                 r.SubjectId == query.PersonId))
            .Where(r => query.HouseholdId is null ||
                (string.Equals(r.SubjectType, RecordSubjectTypes.Household, StringComparison.OrdinalIgnoreCase) &&
                 r.SubjectId == query.HouseholdId))
            .Where(r => string.IsNullOrWhiteSpace(query.Query) || MatchesQuery(r, query.Query))
            .ToList();

        // Fail-closed read filtering at the query boundary: only records the
        // caller may read are returned; nothing reveals the existence or count
        // of unauthorized records (ADR-023). A record is readable when the
        // caller holds the read permission at ANY of its organization scopes.
        var requests = new List<AuthorizationRequest>();
        var slices = new List<(Record Record, int Count)>();
        foreach (var r in filtered)
        {
            var contexts = RecordAuthorization.ContextsFor(r);
            foreach (var context in contexts)
                requests.Add(new AuthorizationRequest(query.ActorId, RecordsPermissions.RecordRead, context));
            slices.Add((r, contexts.Count));
        }

        var decisions = await evaluator.EvaluateBatchAsync(requests, ct);

        var allowed = new List<Record>();
        var index = 0;
        foreach (var (record, count) in slices)
        {
            var slice = decisions.Skip(index).Take(count);
            index += count;
            if (slice.Any(d => d.Allowed))
                allowed.Add(record);
        }

        return allowed
            .OrderByDescending(r => r.UpdatedOn)
            .Select(r => r.ToSummaryDto())
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Free-text match over the record's current non-sensitive field values
    /// (current version fields once verified, working fields otherwise).
    /// Sensitive values are never searched, so the list search cannot reveal
    /// sensitive data (fail-closed).
    /// </summary>
    private static bool MatchesQuery(Record record, string queryText)
    {
        var fields = record.CurrentVersion?.Fields ?? record.WorkingFields;
        return fields.Any(f => !f.IsSensitive &&
            f.FieldValue.Contains(queryText, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record GetRecordQuery(Guid ActorId, Guid RecordId) : IRequest<RecordDto>;

internal sealed class GetRecordQueryHandler(
    IRecordRepository records,
    AuthorizationGuard guard)
    : IRequestHandler<GetRecordQuery, RecordDto>
{
    public async Task<RecordDto> Handle(GetRecordQuery query, CancellationToken ct)
    {
        var record = await LoadOrNothingAsync(records, query.RecordId, ct);

        if (!await RecordAuthorization.HasForRecordAsync(
                guard, query.ActorId, RecordsPermissions.RecordRead, record, ct))
            throw new RecordNotFoundException(query.RecordId);

        return record.ToDto();
    }

    internal static async Task<Record> LoadOrNothingAsync(
        IRecordRepository records, Guid recordId, CancellationToken ct) =>
        await records.GetByIdAsync(recordId, ct)
        ?? throw new RecordNotFoundException(recordId);
}

public sealed record GetRecordSensitiveFieldsQuery(Guid ActorId, Guid RecordId)
    : IRequest<IReadOnlyList<RecordFieldDto>>;

internal sealed class GetRecordSensitiveFieldsQueryHandler(
    IRecordRepository records,
    AuthorizationGuard guard)
    : IRequestHandler<GetRecordSensitiveFieldsQuery, IReadOnlyList<RecordFieldDto>>
{
    public async Task<IReadOnlyList<RecordFieldDto>> Handle(
        GetRecordSensitiveFieldsQuery query, CancellationToken ct)
    {
        var record = await GetRecordQueryHandler.LoadOrNothingAsync(records, query.RecordId, ct);
        await RecordQueryHelpers.GuardSensitiveReadAsync(guard, query, record);

        return record.CurrentVersion is null
            ? Array.Empty<RecordFieldDto>()
            : record.CurrentVersion.Fields
                .Where(f => f.IsSensitive)
                .Select(f => f.ToDto())
                .ToArray();
    }
}

public sealed record ListRecordVersionsQuery(Guid ActorId, Guid RecordId)
    : IRequest<IReadOnlyList<RecordVersionDescriptorDto>>;

internal sealed class ListRecordVersionsQueryHandler(
    IRecordRepository records,
    AuthorizationGuard guard)
    : IRequestHandler<ListRecordVersionsQuery, IReadOnlyList<RecordVersionDescriptorDto>>
{
    public async Task<IReadOnlyList<RecordVersionDescriptorDto>> Handle(
        ListRecordVersionsQuery query, CancellationToken ct)
    {
        var record = await GetRecordQueryHandler.LoadOrNothingAsync(records, query.RecordId, ct);

        if (!await RecordAuthorization.HasForRecordAsync(
                guard, query.ActorId, RecordsPermissions.RecordRead, record, ct))
            throw new RecordNotFoundException(query.RecordId);

        return record.Versions
            .OrderBy(v => v.VersionNumber)
            .Select(v => v.ToDescriptorDto())
            .ToList()
            .AsReadOnly();
    }
}

public sealed record GetRecordVersionQuery(Guid ActorId, Guid RecordId, int VersionNumber)
    : IRequest<RecordVersionDto>;

internal sealed class GetRecordVersionQueryHandler(
    IRecordRepository records,
    AuthorizationGuard guard)
    : IRequestHandler<GetRecordVersionQuery, RecordVersionDto>
{
    public async Task<RecordVersionDto> Handle(GetRecordVersionQuery query, CancellationToken ct)
    {
        var record = await GetRecordQueryHandler.LoadOrNothingAsync(records, query.RecordId, ct);

        if (!await RecordAuthorization.HasForRecordAsync(
                guard, query.ActorId, RecordsPermissions.RecordRead, record, ct))
            throw new RecordNotFoundException(query.RecordId);

        var version = record.Versions.FirstOrDefault(v => v.VersionNumber == query.VersionNumber)
            ?? throw new RecordVersionNotFoundException(query.RecordId, query.VersionNumber);

        return version.ToDto(nonSensitiveOnly: true);
    }
}

public sealed record GetRecordVersionSensitiveFieldsQuery(
    Guid ActorId, Guid RecordId, int VersionNumber) : IRequest<IReadOnlyList<RecordFieldDto>>;

internal sealed class GetRecordVersionSensitiveFieldsQueryHandler(
    IRecordRepository records,
    AuthorizationGuard guard)
    : IRequestHandler<GetRecordVersionSensitiveFieldsQuery, IReadOnlyList<RecordFieldDto>>
{
    public async Task<IReadOnlyList<RecordFieldDto>> Handle(
        GetRecordVersionSensitiveFieldsQuery query, CancellationToken ct)
    {
        var record = await GetRecordQueryHandler.LoadOrNothingAsync(records, query.RecordId, ct);
        await RecordQueryHelpers.GuardSensitiveReadAsync(guard, query, record);

        var version = record.Versions.FirstOrDefault(v => v.VersionNumber == query.VersionNumber)
            ?? throw new RecordVersionNotFoundException(query.RecordId, query.VersionNumber);

        return version.Fields
            .Where(f => f.IsSensitive)
            .Select(f => f.ToDto())
            .ToList()
            .AsReadOnly();
    }
}

public sealed record ListRecordEvidenceQuery(Guid ActorId, Guid RecordId)
    : IRequest<IReadOnlyList<RecordEvidenceReferenceDto>>;

internal sealed class ListRecordEvidenceQueryHandler(
    IRecordRepository records,
    AuthorizationGuard guard)
    : IRequestHandler<ListRecordEvidenceQuery, IReadOnlyList<RecordEvidenceReferenceDto>>
{
    public async Task<IReadOnlyList<RecordEvidenceReferenceDto>> Handle(
        ListRecordEvidenceQuery query, CancellationToken ct)
    {
        var record = await GetRecordQueryHandler.LoadOrNothingAsync(records, query.RecordId, ct);

        if (!await RecordAuthorization.HasForRecordAsync(
                guard, query.ActorId, RecordsPermissions.RecordRead, record, ct))
            throw new RecordNotFoundException(query.RecordId);

        return record.Evidence
            .OrderBy(e => e.AttachedOn)
            .Select(e => e.ToDto())
            .ToList()
            .AsReadOnly();
    }
}

public sealed record ListHoldsQuery(
    Guid ActorId,
    Guid? RecordId,
    string? HoldType,
    string? Status) : IRequest<IReadOnlyList<RecordHoldDto>>;

internal sealed class ListHoldsQueryHandler(
    IRecordRepository records,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator)
    : IRequestHandler<ListHoldsQuery, IReadOnlyList<RecordHoldDto>>
{
    public async Task<IReadOnlyList<RecordHoldDto>> Handle(ListHoldsQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, RecordsPermissions.RecordRead,
            new AuthorizationContext(ResourceType: "record"), ct);

        var activeOnly = string.IsNullOrWhiteSpace(query.Status)
            ? (bool?)null
            : string.Equals(query.Status, "active", StringComparison.OrdinalIgnoreCase);

        var recordsWithHolds = await records.ListByHoldAsync(query.RecordId, query.HoldType, activeOnly, ct);

        // A hold is readable when the caller holds the read permission at ANY
        // of the record's organization scopes (multi-scope, ADR-023).
        var requests = new List<AuthorizationRequest>();
        var slices = new List<(Record Record, int Count)>();
        foreach (var r in recordsWithHolds)
        {
            var contexts = RecordAuthorization.ContextsFor(r);
            foreach (var context in contexts)
                requests.Add(new AuthorizationRequest(query.ActorId, RecordsPermissions.RecordRead, context));
            slices.Add((r, contexts.Count));
        }

        var decisions = await evaluator.EvaluateBatchAsync(requests, ct);

        var readable = new List<Record>();
        var index = 0;
        foreach (var (record, count) in slices)
        {
            var slice = decisions.Skip(index).Take(count);
            index += count;
            if (slice.Any(d => d.Allowed))
                readable.Add(record);
        }

        return readable
            .SelectMany(r => r.Holds
                .Where(h => query.HoldType is null ||
                    string.Equals(h.HoldType, query.HoldType, StringComparison.OrdinalIgnoreCase))
                .Where(h => activeOnly is null || h.IsActive == activeOnly)
                .Select(h => h.ToDto(r.Id)))
            .OrderByDescending(h => h.PlacedOn)
            .ToList()
            .AsReadOnly();
    }
}

public sealed record GetHoldQuery(Guid ActorId, Guid HoldId) : IRequest<RecordHoldDto>;

internal sealed class GetHoldQueryHandler(
    IRecordRepository records,
    AuthorizationGuard guard)
    : IRequestHandler<GetHoldQuery, RecordHoldDto>
{
    public async Task<RecordHoldDto> Handle(GetHoldQuery query, CancellationToken ct)
    {
        var record = await records.GetByHoldIdAsync(query.HoldId, ct)
            ?? throw new RecordNotFoundException(query.HoldId);

        if (!await RecordAuthorization.HasForRecordAsync(
                guard, query.ActorId, RecordsPermissions.RecordRead, record, ct))
            throw new RecordNotFoundException(query.HoldId);

        var hold = record.Holds.FirstOrDefault(h => h.Id == query.HoldId)
            ?? throw new RecordHoldNotFoundException(query.HoldId);

        var maySeeReason = await RecordAuthorization.HasForRecordAsync(
            guard, query.ActorId, RecordsPermissions.HoldManage, record, ct);

        return hold.ToDto(record.Id, exposeReason: maySeeReason);
    }
}

public sealed record ListRetentionSchedulesQuery(Guid ActorId)
    : IRequest<IReadOnlyList<RetentionScheduleDto>>;

internal sealed class ListRetentionSchedulesQueryHandler(
    IRetentionScheduleRepository retention,
    AuthorizationGuard guard)
    : IRequestHandler<ListRetentionSchedulesQuery, IReadOnlyList<RetentionScheduleDto>>
{
    public async Task<IReadOnlyList<RetentionScheduleDto>> Handle(
        ListRetentionSchedulesQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, RecordsPermissions.RecordRead,
            new AuthorizationContext(ResourceType: "record"), ct);

        return (await retention.ListAsync(ct))
            .OrderBy(s => s.Code)
            .Select(s => s.ToDto())
            .ToList()
            .AsReadOnly();
    }
}

public sealed record GetRetentionScheduleQuery(Guid ActorId, string Code)
    : IRequest<RetentionScheduleDto>;

internal sealed class GetRetentionScheduleQueryHandler(
    IRetentionScheduleRepository retention,
    AuthorizationGuard guard)
    : IRequestHandler<GetRetentionScheduleQuery, RetentionScheduleDto>
{
    public async Task<RetentionScheduleDto> Handle(GetRetentionScheduleQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, RecordsPermissions.RecordRead,
            new AuthorizationContext(ResourceType: "record"), ct);

        var schedule = await retention.GetByCodeAsync(query.Code, ct)
            ?? throw new RetentionScheduleNotFoundException(query.Code);

        return schedule.ToDto();
    }
}

public sealed record ListRecordCategoriesQuery(Guid ActorId)
    : IRequest<IReadOnlyList<RecordCategoryDto>>;

internal sealed class ListRecordCategoriesQueryHandler(
    IRecordCategoryRepository categories,
    AuthorizationGuard guard)
    : IRequestHandler<ListRecordCategoriesQuery, IReadOnlyList<RecordCategoryDto>>
{
    public async Task<IReadOnlyList<RecordCategoryDto>> Handle(
        ListRecordCategoriesQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, RecordsPermissions.RecordRead,
            new AuthorizationContext(ResourceType: "record"), ct);

        return (await categories.ListAsync(ct))
            .OrderBy(c => c.Code)
            .Select(c => c.ToDto())
            .ToList()
            .AsReadOnly();
    }
}

internal static class RecordQueryHelpers
{
    /// <summary>
    /// Sensitive-field reads require both the base read permission and the
    /// sensitive capability; any failure surfaces as 404 (no existence oracle).
    /// Both capabilities are evaluated at any of the record's scopes.
    /// </summary>
    public static async Task GuardSensitiveReadAsync(
        AuthorizationGuard guard, GetRecordSensitiveFieldsQuery query, Record record)
    {
        if (!await RecordAuthorization.HasForRecordAsync(
                guard, query.ActorId, RecordsPermissions.RecordRead, record, CancellationToken.None) ||
            !await RecordAuthorization.HasForRecordAsync(
                guard, query.ActorId, RecordsPermissions.RecordReadSensitive, record, CancellationToken.None))
            throw new RecordNotFoundException(query.RecordId);
    }

    public static async Task GuardSensitiveReadAsync(
        AuthorizationGuard guard, GetRecordVersionSensitiveFieldsQuery query, Record record)
    {
        if (!await RecordAuthorization.HasForRecordAsync(
                guard, query.ActorId, RecordsPermissions.RecordRead, record, CancellationToken.None) ||
            !await RecordAuthorization.HasForRecordAsync(
                guard, query.ActorId, RecordsPermissions.RecordReadSensitive, record, CancellationToken.None))
            throw new RecordNotFoundException(query.RecordId);
    }
}